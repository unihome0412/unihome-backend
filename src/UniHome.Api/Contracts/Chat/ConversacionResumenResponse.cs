using UniHome.Application.Chat.ListConversations;

namespace UniHome.Api.Contracts.Chat;

/// <summary>EP-M08-03 (HU-069): fila del listado de "mis conversaciones".</summary>
public sealed record ConversacionResumenResponse(
    Guid ConversacionId,
    Guid UnidadId,
    string TipoUnidad,
    decimal PrecioMensualPorLugarMxn,
    Guid OtroAccountId,
    string OtroNombreCompleto,
    string? UltimoMensaje,
    DateTimeOffset UltimaActividad)
{
    public static ConversacionResumenResponse FromView(ConversationSummaryView view) =>
        new(
            view.ConversationId,
            view.UnitId,
            view.TipoUnidad,
            view.PrecioMensualPorLugarMxn,
            view.OtroAccountId,
            view.OtroNombreCompleto,
            view.UltimoMensaje,
            view.UltimaActividad);
}
