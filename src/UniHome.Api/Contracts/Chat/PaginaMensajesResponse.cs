using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Chat.ListMessages;
using UniHome.Application.Chat.SendMessage;

namespace UniHome.Api.Contracts.Chat;

public sealed record PaginaMensajesResponse(
    IReadOnlyList<MensajeItemResponse> Mensajes,
    CursorMensajesResponse? SiguienteCursor,
    string Aviso)
{
    public static PaginaMensajesResponse FromView(MessagePageView view) =>
        new(
            view.Mensajes.Select(MensajeItemResponse.FromView).ToArray(),
            view.SiguienteCursor is null ? null : CursorMensajesResponse.FromCursor(view.SiguienteCursor),
            ChatNotices.EncryptedInTransit);
}

/// <summary>
/// Cursor compuesto (fecha, id) para seguir paginando hacia atrás con
/// <c>?antes=Fecha&amp;antesId=MensajeId</c>. Solo la fecha no basta: dos
/// mensajes con la misma fecha podrían saltarse en el corte de página.
/// </summary>
public sealed record CursorMensajesResponse(DateTimeOffset Fecha, Guid MensajeId)
{
    public static CursorMensajesResponse FromCursor(MessageCursor cursor) =>
        new(cursor.SentAt, cursor.MessageId);
}

public sealed record MensajeItemResponse(
    Guid Id,
    Guid ConversacionId,
    Guid RemitenteAccountId,
    string Texto,
    DateTimeOffset EnviadoEn)
{
    public static MensajeItemResponse FromView(MessageView view) =>
        new(view.MensajeId, view.ConversacionId, view.RemitenteAccountId, view.Texto, view.EnviadoEn);
}
