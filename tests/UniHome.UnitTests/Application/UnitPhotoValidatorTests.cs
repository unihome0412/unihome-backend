using UniHome.Application.Units.Photos;

namespace UniHome.UnitTests.Application;

public sealed class UnitPhotoValidatorTests
{
    [Fact]
    public void Validate_PngValido_DebeDetectarTipoYHash()
    {
        var result = UnitPhotoValidator.Validate(new UnitPhotoUpload(
            "foto.png",
            "application/octet-stream",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(9, result.SizeBytes);
        Assert.False(string.IsNullOrWhiteSpace(result.Sha256));
    }

    [Fact]
    public void Validate_JpegValido_DebeAceptarse()
    {
        var result = UnitPhotoValidator.Validate(new UnitPhotoUpload(
            "foto.jpg",
            "image/jpeg",
            [0xFF, 0xD8, 0xFF, 0x00]));

        Assert.Equal("image/jpeg", result.ContentType);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x01 })]
    [InlineData(new byte[] { 0x89, 0x50 })]
    public void Validate_ContenidoNoReconocido_DebeRechazarse(byte[] content)
    {
        Assert.Throws<ArgumentException>(() =>
            UnitPhotoValidator.Validate(new UnitPhotoUpload("foto.jpg", "image/jpeg", content)));
    }

    [Fact]
    public void Validate_Vacio_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            UnitPhotoValidator.Validate(new UnitPhotoUpload("foto.png", "image/png", [])));
    }

    [Fact]
    public void Validate_ExcedeTamano_DebeRechazarse()
    {
        var content = new byte[UnitPhotoValidator.MaxBytesPerPhoto + 1];
        content[0] = 0x89;
        content[1] = 0x50;
        content[2] = 0x4E;
        content[3] = 0x47;
        content[4] = 0x0D;
        content[5] = 0x0A;
        content[6] = 0x1A;
        content[7] = 0x0A;

        Assert.Throws<ArgumentException>(() =>
            UnitPhotoValidator.Validate(new UnitPhotoUpload("foto.png", "image/png", content)));
    }
}
