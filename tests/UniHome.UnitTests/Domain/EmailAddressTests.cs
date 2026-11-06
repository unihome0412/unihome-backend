using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.Domain;

public sealed class EmailAddressTests
{
    [Fact]
    public void Create_NormalizaMayusculasYEspacios()
    {
        var email = EmailAddress.Create("  Ana@Uni.EDU  ");

        Assert.Equal("ana@uni.edu", email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("@uni.edu")]
    [InlineData("Nombre <ana@uni.edu>")]
    [InlineData("ana @uni.edu")]
    public void Create_RechazaCorreosInvalidos(string? value)
    {
        Assert.Throws<ArgumentException>(() => EmailAddress.Create(value));
    }

    [Fact]
    public void Igualdad_UsaElCorreoNormalizado()
    {
        var first = EmailAddress.Create("Ana@Uni.EDU");
        var second = EmailAddress.Create("ana@uni.edu");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
