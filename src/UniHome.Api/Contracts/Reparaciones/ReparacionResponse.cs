using UniHome.Application.Reparaciones;
using UniHome.Domain.Reparaciones;

namespace UniHome.Api.Contracts.Reparaciones;

public sealed record ReparacionFotoResponse(string ContentType, long SizeBytes)
{
    public static ReparacionFotoResponse FromDomain(ReparacionFoto photo) =>
        new(photo.ContentType, photo.SizeBytes);
}

public sealed record ReparacionResponse(
    Guid Id,
    Guid UnitId,
    string Titulo,
    string Descripcion,
    ReparacionEstado Estado,
    ReparacionFotoResponse? Foto,
    Guid ReportadoPorAccountId,
    string? ReportadoPorNombreCompleto,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn)
{
    public static ReparacionResponse FromDomain(Reparacion reparacion) =>
        new(
            reparacion.Id,
            reparacion.UnitId,
            reparacion.Title,
            reparacion.Description,
            reparacion.Status,
            reparacion.Photo is null ? null : ReparacionFotoResponse.FromDomain(reparacion.Photo),
            reparacion.ReportedByAccountId,
            ReportadoPorNombreCompleto: null,
            reparacion.CreatedAt,
            reparacion.UpdatedAt);

    public static ReparacionResponse FromView(ReparacionView view) =>
        new(
            view.Reparacion.Id,
            view.Reparacion.UnitId,
            view.Reparacion.Title,
            view.Reparacion.Description,
            view.Reparacion.Status,
            view.Reparacion.Photo is null ? null : ReparacionFotoResponse.FromDomain(view.Reparacion.Photo),
            view.Reparacion.ReportedByAccountId,
            view.ReportedByFullName,
            view.Reparacion.CreatedAt,
            view.Reparacion.UpdatedAt);
}
