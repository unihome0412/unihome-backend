using System.Security.Cryptography;

namespace UniHome.Application.Units.Photos;

public sealed record ValidatedUnitPhoto(
    byte[] Content,
    string ContentType,
    long SizeBytes,
    string Sha256);

/// <summary>
/// Valida el contenido real de una foto: tamaño, firma JPEG/PNG y hash. No confía
/// en la extensión ni en el Content-Type declarado por el cliente.
/// </summary>
public static class UnitPhotoValidator
{
    public const long MaxBytesPerPhoto = 5 * 1024 * 1024;

    public static ValidatedUnitPhoto Validate(UnitPhotoUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);

        if (upload.Content is null || upload.Content.Length == 0)
        {
            throw new ArgumentException("La fotografía está vacía.", nameof(upload));
        }

        if (upload.Content.LongLength > MaxBytesPerPhoto)
        {
            throw new ArgumentException(
                $"Cada fotografía debe pesar como máximo {MaxBytesPerPhoto / (1024 * 1024)} MB.",
                nameof(upload));
        }

        var contentType = DetectContentType(upload.Content)
            ?? throw new ArgumentException(
                "Solo se permiten fotografías JPG o PNG con contenido válido.",
                nameof(upload));

        return new ValidatedUnitPhoto(
            upload.Content,
            contentType,
            upload.Content.LongLength,
            Convert.ToHexString(SHA256.HashData(upload.Content)));
    }

    private static string? DetectContentType(byte[] content)
    {
        if (content.Length >= 3 &&
            content[0] == 0xFF &&
            content[1] == 0xD8 &&
            content[2] == 0xFF)
        {
            return "image/jpeg";
        }

        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        return content.Length >= png.Length && content.AsSpan(0, png.Length).SequenceEqual(png)
            ? "image/png"
            : null;
    }
}
