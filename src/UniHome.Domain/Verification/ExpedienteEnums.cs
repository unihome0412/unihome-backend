namespace UniHome.Domain.Verification;

/// <summary>
/// Estado del expediente de verificación. El indicador "Sospechoso" es un
/// atributo separado y nunca un estado (ARQ-003, HU-021/HU-022).
/// </summary>
public enum EstadoExpediente
{
    Pendiente,
    EnRevision,
    Aprobado,
    Rechazado
}

public enum TipoDocumentoVerificacion
{
    Ine,
    Comprobante
}

/// <summary>
/// Comparación orientativa de nombres (HU-019). <see cref="NoDisponible"/> se
/// conserva para OCR ausente o texto insuficiente; nunca decide por sí sola.
/// </summary>
public enum ResultadoComparacionNombres
{
    CoincidenciaOrientativa,
    DiferenciaDetectada,
    NoDisponible
}

public enum EstadoAnalisisDocumento
{
    Pendiente,
    Completado,
    NoDisponible,
    Fallido
}
