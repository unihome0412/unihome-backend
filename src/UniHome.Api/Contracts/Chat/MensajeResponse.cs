using UniHome.Application.Chat.SendMessage;

namespace UniHome.Api.Contracts.Chat;

public sealed record MensajeResponse(
    Guid Id,
    Guid ConversacionId,
    Guid RemitenteAccountId,
    string Texto,
    DateTimeOffset EnviadoEn,
    string Aviso)
{
    public static MensajeResponse FromView(MessageView view) =>
        new(
            view.MensajeId,
            view.ConversacionId,
            view.RemitenteAccountId,
            view.Texto,
            view.EnviadoEn,
            ChatNotices.EncryptedInTransit);
}
