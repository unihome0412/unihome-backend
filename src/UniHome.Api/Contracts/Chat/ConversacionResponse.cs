using UniHome.Application.Chat.StartConversation;

namespace UniHome.Api.Contracts.Chat;

public sealed record ConversacionResponse(
    Guid ConversacionId,
    Guid UnidadId,
    Guid OtroAccountId,
    string OtroNombreCompleto,
    DateTimeOffset CreadaEn)
{
    public static ConversacionResponse FromView(ConversationView view) =>
        new(view.ConversationId, view.UnitId, view.OtroAccountId, view.OtroNombreCompleto, view.CreadaEn);
}
