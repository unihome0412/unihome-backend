using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Documents;

public sealed record VerificationDocumentUpload(
    string? OriginalFileName,
    string? DeclaredContentType,
    byte[] Content);

public sealed record ValidatedVerificationDocument(
    TipoDocumentoVerificacion Type,
    byte[] Content,
    string ContentType,
    string OriginalFileName,
    long SizeBytes,
    string Sha256);

/// <summary>
/// Endurecimiento de carga documental: valida firma binaria real, extensión,
/// MIME declarado, tamaño, nombre y ausencia de contenido activo PDF. No confía
/// en lo que declara el cliente ni en el nombre del archivo.
/// </summary>
public static partial class VerificationDocumentValidator
{
    public const long MaxBytesPerDocument = 10 * 1024 * 1024;
    public const long MaxRequestBytes = MaxBytesPerDocument + (1024 * 1024);

    public static ValidatedVerificationDocument Validate(
        TipoDocumentoVerificacion type,
        VerificationDocumentUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(upload.Content);

        if (upload.Content.Length == 0)
        {
            throw new VerificationDocumentValidationException("El archivo está vacío.");
        }

        if (upload.Content.LongLength > MaxBytesPerDocument)
        {
            throw new VerificationDocumentValidationException(
                $"Cada documento debe pesar como máximo {MaxBytesPerDocument / (1024 * 1024)} MB.");
        }

        var (contentType, extension) = Detect(upload.Content);
        var originalName = NormalizeFileName(upload.OriginalFileName);
        var providedExtension = Path.GetExtension(originalName).ToLowerInvariant();

        if (!string.IsNullOrEmpty(providedExtension) &&
            !string.Equals(providedExtension, extension, StringComparison.Ordinal) &&
            !(providedExtension == ".jpeg" && extension == ".jpg"))
        {
            throw new VerificationDocumentValidationException(
                "La extensión del archivo no coincide con su contenido real.");
        }

        var declared = upload.DeclaredContentType?.Split(';')[0].Trim();

        if (!string.IsNullOrWhiteSpace(declared) &&
            !string.Equals(declared, "application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(declared, contentType, StringComparison.OrdinalIgnoreCase) &&
            !(string.Equals(declared, "image/jpg", StringComparison.OrdinalIgnoreCase) &&
              string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)))
        {
            throw new VerificationDocumentValidationException(
                "El tipo de contenido declarado no coincide con el contenido real.");
        }

        if (contentType == "application/pdf")
        {
            ValidatePdf(upload.Content);
        }

        return new ValidatedVerificationDocument(
            type,
            upload.Content,
            contentType,
            originalName,
            upload.Content.LongLength,
            Convert.ToHexString(SHA256.HashData(upload.Content)));
    }

    private static (string ContentType, string Extension) Detect(byte[] content)
    {
        if (content.Length >= 3 &&
            content[0] == 0xFF &&
            content[1] == 0xD8 &&
            content[2] == 0xFF)
        {
            return ("image/jpeg", ".jpg");
        }

        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        if (content.Length >= png.Length && content.AsSpan(0, png.Length).SequenceEqual(png))
        {
            return ("image/png", ".png");
        }

        ReadOnlySpan<byte> pdf = "%PDF-"u8;

        if (content.AsSpan(0, Math.Min(content.Length, 1024)).IndexOf(pdf) >= 0)
        {
            return ("application/pdf", ".pdf");
        }

        throw new VerificationDocumentValidationException(
            "Solo se permiten documentos JPG, PNG o PDF con contenido válido.");
    }

    private static void ValidatePdf(byte[] content)
    {
        ReadOnlySpan<byte> eof = "%%EOF"u8;
        var tailStart = Math.Max(0, content.Length - 4096);

        if (content.AsSpan(tailStart).IndexOf(eof) < 0)
        {
            throw new VerificationDocumentValidationException(
                "El PDF está incompleto o dañado.");
        }

        // No se ejecuta ni renderiza el PDF en el servidor, pero se rechaza
        // contenido activo que un visor podría interpretar.
        var probe = System.Text.Encoding.Latin1.GetString(
            content,
            0,
            Math.Min(content.Length, 2 * 1024 * 1024));

        if (ActivePdfContentRegex().IsMatch(probe))
        {
            throw new VerificationDocumentValidationException(
                "El PDF contiene contenido activo no permitido.");
        }
    }

    private static string NormalizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "documento";
        }

        var trimmed = value.Trim();

        if (trimmed.Length > DocumentoVerificacion.MaxOriginalFileNameLength ||
            trimmed.Any(char.IsControl) ||
            !string.Equals(Path.GetFileName(trimmed), trimmed, StringComparison.Ordinal) ||
            trimmed.IndexOfAny(['/', '\\', '"', '\'', '<', '>', '|', ':', '*', '?']) >= 0)
        {
            throw new VerificationDocumentValidationException(
                "El nombre del archivo no es válido.");
        }

        return trimmed;
    }

    [GeneratedRegex(
        @"/(JavaScript|JS|Launch|EmbeddedFile)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex ActivePdfContentRegex();
}

public sealed class VerificationDocumentValidationException : Exception
{
    public VerificationDocumentValidationException(string message)
        : base(message)
    {
    }
}
