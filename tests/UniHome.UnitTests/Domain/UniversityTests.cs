using UniHome.Domain.Universities;

namespace UniHome.UnitTests.Domain;

public sealed class UniversityTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ConNombreYCampus_DebeQuedarActivaYNormalizada()
    {
        var university = University.Create("  Universidad   Tecnológica de Monterrey  ", Now);

        Assert.NotEqual(Guid.Empty, university.Id);
        Assert.Equal("Universidad Tecnológica de Monterrey", university.NameAndCampus);
        Assert.Equal("universidad tecnologica de monterrey", university.NormalizedName);
        Assert.True(university.IsActive);
        Assert.Equal(Now, university.CreatedAt);
    }

    [Theory]
    [InlineData("Tecnológico de Monterrey", "tecnologico de monterrey")]
    [InlineData("UNIVERSIDAD AUTÓNOMA DE MÉXICO", "universidad autonoma de mexico")]
    [InlineData("  Campus   Norte  ", "campus norte")]
    public void Normalize_DebeQuitarAcentosMayusculasYEspaciosSobrantes(
        string value,
        string expected)
    {
        Assert.Equal(expected, University.Normalize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SinNombreYCampus_DebeRechazarse(string? value)
    {
        Assert.Throws<ArgumentException>(() => University.Create(value, Now));
    }

    [Fact]
    public void Create_ConLongitudExcesiva_DebeRechazarse()
    {
        var value = new string('a', University.MaxNameAndCampusLength + 1);

        Assert.Throws<ArgumentException>(() => University.Create(value, Now));
    }

    [Fact]
    public void Restore_ConservaEstadoInactivoYFecha()
    {
        var id = Guid.NewGuid();
        var createdAt = Now.AddDays(-2);

        var university = University.Restore(
            id,
            "Universidad del Norte",
            isActive: false,
            createdAt);

        Assert.Equal(id, university.Id);
        Assert.False(university.IsActive);
        Assert.Equal(createdAt, university.CreatedAt);
        Assert.Equal("universidad del norte", university.NormalizedName);
    }

    [Fact]
    public void Restore_ConIdentificadorVacio_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            University.Restore(Guid.Empty, "Universidad", true, Now));
    }

    [Fact]
    public void Rename_DebeActualizarNombreYNormalizacion()
    {
        var university = University.Create("Universidad del Norte", Now);

        university.Rename("  Universidad Autónoma   del Norte  ");

        Assert.Equal("Universidad Autónoma del Norte", university.NameAndCampus);
        Assert.Equal("universidad autonoma del norte", university.NormalizedName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_SinNombreYCampus_DebeRechazarse(string? value)
    {
        var university = University.Create("Universidad del Norte", Now);

        Assert.Throws<ArgumentException>(() => university.Rename(value));
        Assert.Equal("Universidad del Norte", university.NameAndCampus);
    }

    [Fact]
    public void SetActive_DebeCambiarEstadoSinBorrar()
    {
        var university = University.Create("Universidad del Norte", Now);

        university.SetActive(false);

        Assert.False(university.IsActive);
        Assert.Equal("Universidad del Norte", university.NameAndCampus);
    }
}
