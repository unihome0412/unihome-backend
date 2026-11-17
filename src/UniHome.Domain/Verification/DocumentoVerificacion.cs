using UniHome.Domain.Common;

namespace UniHome.Domain.Verification;

/// <summary>
/// Metadatos de un documento recibido. El binario cifrado vive en el
/// almacenamiento privado y nunca se expone desde esta entidad. El nombre OCR es
/// un indicador best-effort: si el motor no está disponible queda nulo.
/// </summary>
public sealed class DocumentoVerificacion
{
    public const int MaxOriginalFileNameLength = 255;
    public const int MaxOcrNameLength = 200;
    public const int MaxStorageKeyLength = 128;

    private readonly List<string> _editionSignals;

    private DocumentoVerificacion(
        TipoDocumentoVerificacion tipo,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        int version,
        DateTimeOffset uploadedAt,
        string? nombreOcr,
        EstadoAnalisisDocumento estadoOcr,
        EstadoAnalisisDocumento estadoEdicion,
        bool edicionDetectada,
        bool elaDisponible,
        bool metadatosDisponibles,
        IEnumerable<string> editionSignals,
        string? detalleAnalisis)
    {
        Tipo = tipo;
        StorageKey = storageKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        Version = version;
        UploadedAt = uploadedAt;
        NombreOcr = nombreOcr;
        EstadoOcr = estadoOcr;
        EstadoEdicion = estadoEdicion;
        EdicionDetectada = edicionDetectada;
        ElaDisponible = elaDisponible;
        MetadatosDisponibles = metadatosDisponibles;
        _editionSignals = [.. editionSignals];
        DetalleAnalisis = detalleAnalisis;
    }

    public TipoDocumentoVerificacion Tipo { get; }

    public string StorageKey { get; }

    public string OriginalFileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public string Sha256 { get; }

    public int Version { get; }

    public DateTimeOffset UploadedAt { get; }

    public string? NombreOcr { get; }

    public EstadoAnalisisDocumento EstadoOcr { get; }

    public EstadoAnalisisDocumento EstadoEdicion { get; }

    public bool EdicionDetectada { get; }

    public bool ElaDisponible { get; }

    public bool MetadatosDisponibles { get; }

    public IReadOnlyList<string> EditionSignals => _editionSignals.AsReadOnly();

    /// <summary>Motivo seguro (sin contenido) cuando el análisis no estuvo disponible.</summary>
    public string? DetalleAnalisis { get; }

    public bool RevisionAutomaticaCompletada =>
        EstadoOcr != EstadoAnalisisDocumento.Pendiente &&
        EstadoEdicion != EstadoAnalisisDocumento.Pendiente;

    public static DocumentoVerificacion Create(
        TipoDocumentoVerificacion tipo,
        string? storageKey,
        string? originalFileName,
        string? contentType,
        long sizeBytes,
        string? sha256,
        int version,
        DateTimeOffset uploadedAt)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new ArgumentException("El tipo de documento no es válido.", nameof(tipo));
        }

        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > MaxStorageKeyLength)
        {
            throw new ArgumentException("La referencia de almacenamiento no es válida.", nameof(storageKey));
        }

        var safeName = NormalizeFileName(originalFileName);

        if (contentType is not ("application/pdf" or "image/jpeg" or "image/png"))
        {
            throw new ArgumentException("El tipo de contenido del documento no es válido.", nameof(contentType));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentException("El documento está vacío.", nameof(sizeBytes));
        }

        if (string.IsNullOrWhiteSpace(sha256) ||
            sha256.Length != 64 ||
            !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("La huella del documento no es válida.", nameof(sha256));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        return new DocumentoVerificacion(
            tipo,
            storageKey,
            safeName,
            contentType,
            sizeBytes,
            sha256.ToUpperInvariant(),
            version,
            uploadedAt,
            nombreOcr: null,
            EstadoAnalisisDocumento.Pendiente,
             EstadoAnalisisDocumento.Pendiente,
             edicionDetectada: false,
             elaDisponible: false,
             metadatosDisponibles: false,
             editionSignals: [],
             detalleAnalisis: null);
    }

    public static DocumentoVerificacion Restore(
        TipoDocumentoVerificacion tipo,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        int version,
        DateTimeOffset uploadedAt,
        string? nombreOcr,
        EstadoAnalisisDocumento estadoOcr,
        EstadoAnalisisDocumento estadoEdicion,
        bool edicionDetectada,
        bool elaDisponible,
        bool metadatosDisponibles,
        IEnumerable<string>? editionSignals,
        string? detalleAnalisis) =>
        new(
            tipo,
            storageKey,
            NormalizeFileName(originalFileName),
            contentType,
            sizeBytes,
            sha256.ToUpperInvariant(),
            version,
            uploadedAt,
            NormalizeOcrName(nombreOcr),
            estadoOcr,
            estadoEdicion,
            edicionDetectada,
            elaDisponible,
            metadatosDisponibles,
            (editionSignals ?? []).Take(IndicadorRiesgo.MaxSignals),
            detalleAnalisis);

    public DocumentoVerificacion WithAnalysis(
        string? nombreOcr,
        EstadoAnalisisDocumento estadoOcr,
        EstadoAnalisisDocumento estadoEdicion,
        bool edicionDetectada,
        bool elaDisponible,
        bool metadatosDisponibles,
        IEnumerable<string>? editionSignals,
        string? detalleAnalisis) =>
        new(
            Tipo,
            StorageKey,
            OriginalFileName,
            ContentType,
            SizeBytes,
            Sha256,
            Version,
            UploadedAt,
            NormalizeOcrName(nombreOcr),
            estadoOcr,
            estadoEdicion,
            edicionDetectada,
            elaDisponible,
            metadatosDisponibles,
            (editionSignals ?? []).Take(IndicadorRiesgo.MaxSignals),
            detalleAnalisis);

    private static string NormalizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "documento";
        }

        // No depender de Path.GetFileName: en Linux '\' no es separador y una
        // ruta Windows se conservaría completa. Se normalizan ambos separadores
        // y se conserva únicamente el nombre base, sin exponer la ruta original.
        var candidate = value.Trim().Replace('\\', '/');
        var lastSeparator = candidate.LastIndexOf('/');

        if (lastSeparator >= 0)
        {
            candidate = candidate[(lastSeparator + 1)..];
        }

        // Caso de unidad relativa de Windows sin separador, por ejemplo C:ine.png.
        if (candidate.Length >= 2 &&
            char.IsAsciiLetter(candidate[0]) &&
            candidate[1] == ':')
        {
            candidate = candidate[2..].TrimStart('/', '\\');
        }

        var normalized = new string(candidate
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();

        if (normalized.Length == 0 || normalized is "." or "..")
        {
            return "documento";
        }

        return normalized.Length > MaxOriginalFileNameLength
            ? normalized[..MaxOriginalFileNameLength]
            : normalized;
    }

    private static string? NormalizeOcrName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = TextNormalization.CollapseWhitespace(
            new string(value.Where(character => !char.IsControl(character)).ToArray()));

        if (normalized.Length < 2)
        {
            return null;
        }

        return normalized.Length > MaxOcrNameLength
            ? normalized[..MaxOcrNameLength]
            : normalized;
    }
}
