using UniHome.Application.Verification.Admin;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Verification;

namespace UniHome.Api.Contracts.Verification;

public sealed record DocumentosRecibidosResponse(bool Ine, bool Comprobante);

public sealed record ExpedienteDecisionResponse(
    EstadoExpediente Estado,
    DateTimeOffset Fecha,
    string? Motivo);

public sealed record MiExpedienteResponse(
    Guid Id,
    string Folio,
    EstadoExpediente Estado,
    DocumentosRecibidosResponse DocumentosRecibidos,
    bool RevisionAutomaticaCompletada,
    bool DecisionPendiente,
    DateTimeOffset FechaSolicitud,
    string AvisoAlmacenamiento,
    ExpedienteDecisionResponse? Decision)
{
    public const string StorageNotice =
        "Almacenamiento cifrado y acceso restringido.";

    public static MiExpedienteResponse FromView(MiExpedienteView view) =>
        new(
            view.Id,
            view.Folio,
            view.Estado,
            new DocumentosRecibidosResponse(
                view.DocumentosRecibidos.Ine,
                view.DocumentosRecibidos.Comprobante),
            view.RevisionAutomaticaCompletada,
            view.DecisionPendiente,
            view.FechaSolicitud,
            StorageNotice,
            view.Decision is null
                ? null
                : new ExpedienteDecisionResponse(
                    view.Decision.Estado,
                    view.Decision.Fecha,
                    view.Decision.Motivo));
}

public sealed record ExpedienteResumenResponse(
    Guid Id,
    string Folio,
    string SolicitanteNombre,
    DateTimeOffset Fecha,
    EstadoExpediente Estado,
    bool EsSospechoso)
{
    public static ExpedienteResumenResponse FromView(ExpedienteResumenView view) =>
        new(
            view.Id,
            view.Folio,
            view.SolicitanteNombre,
            view.Fecha,
            view.Estado,
            view.EsSospechoso);
}

public sealed record IndicadorEdicionResponse(
    bool Detectado,
    bool Disponible,
    string Aviso,
    bool ElaDisponible,
    bool MetadatosDisponibles,
    IReadOnlyList<string> Detalle)
{
    public static IndicadorEdicionResponse FromView(IndicadorEdicionView view) =>
        new(
            view.Detectado,
            view.Disponible,
            view.Aviso,
            view.ElaDisponible,
            view.MetadatosDisponibles,
            view.Detalle);
}

public sealed record ExpedienteDetalleResponse(
    Guid Id,
    string Folio,
    string SolicitanteNombre,
    string? SolicitanteCorreo,
    DateTimeOffset Fecha,
    EstadoExpediente Estado,
    string? IneUrlMiniatura,
    string? ComprobanteUrlMiniatura,
    string? NombreOcrIne,
    string? NombreOcrComprobante,
    ResultadoComparacionNombres ResultadoComparacionNombres,
    IndicadorEdicionResponse IndicadorEdicion,
    bool EsSospechoso,
    IReadOnlyList<string> SenalesRiesgo,
    bool RevisionAutomaticaCompletada,
    string? MotivoRechazo)
{
    public static ExpedienteDetalleResponse FromView(ExpedienteDetalleView view) =>
        new(
            view.Id,
            view.Folio,
            view.SolicitanteNombre,
            view.SolicitanteCorreo,
            view.Fecha,
            view.Estado,
            view.IneUrlMiniatura,
            view.ComprobanteUrlMiniatura,
            view.NombreOcrIne,
            view.NombreOcrComprobante,
            view.ResultadoComparacionNombres,
            IndicadorEdicionResponse.FromView(view.IndicadorEdicion),
            view.EsSospechoso,
            view.SenalesRiesgo,
            view.RevisionAutomaticaCompletada,
            view.MotivoRechazo);
}

public sealed record DocumentoUrlResponse(
    string Url,
    DateTimeOffset ExpiraEn,
    TipoDocumentoVerificacion Tipo)
{
    public static DocumentoUrlResponse FromView(DocumentAccessUrlView view) =>
        new(view.Url, view.ExpiraEn, view.Tipo);
}
