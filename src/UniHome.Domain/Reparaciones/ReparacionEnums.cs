namespace UniHome.Domain.Reparaciones;

/// <summary>EP-M11 (HU-065): estado de seguimiento de un reporte de reparación.</summary>
public enum ReparacionEstado
{
    Pendiente,
    EnProceso,
    Atendido
}

/// <summary>EP-M11 (HU-063): metadatos de la fotografía opcional adjunta al reporte. El binario vive en Cloudinary.</summary>
public sealed record ReparacionFoto(string StorageKey, string ContentType, long SizeBytes, string Sha256);
