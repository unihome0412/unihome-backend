using UniHome.Application.Chat.ListMessages;
using UniHome.Application.Chat.SendMessage;

namespace UniHome.Api.Contracts.Chat;

public sealed record PaginaMensajesResponse(
    IReadOnlyList<MensajeItemResponse> Mensajes,
    DateTimeOffset? SiguienteCursor,
    string Aviso)
{
    public static PaginaMensajesResponse FromView(MessagePageView view) =>
        new(
            view.Mensajes.Select(MensajeItemResponse.FromView).ToArray(),
            view.SiguienteCursor,
            ChatNotices.EncryptedInTransit);
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
