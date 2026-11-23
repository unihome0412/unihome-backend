using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Chat;
using UniHome.Domain.Properties;

namespace UniHome.Application.Chat.StartConversation;

/// <summary>
/// EP-M08-01 (HU-067): abre una conversación entre quien contacta desde la
/// ficha de una unidad aprobada y el propietario de esa unidad. No se puede
/// contactar la propia unidad, y si ya existe una conversación para esa unidad
/// y ese par de cuentas, se reutiliza en vez de crear una nueva.
/// </summary>
public sealed class StartConversationService
{
    private readonly IUnitRepository _units;
    private readonly IConversationRepository _conversations;
    private readonly IAccountRepository _accounts;
    private readonly ISystemClock _clock;

    public StartConversationService(
        IUnitRepository units,
        IConversationRepository conversations,
        IAccountRepository accounts,
        ISystemClock clock)
    {
        _units = units;
        _conversations = conversations;
        _accounts = accounts;
        _clock = clock;
    }

    public async Task<OperationResult<ConversationView>> StartAsync(
        Guid requesterAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        // Igual que la ficha pública (EP-M04-04): una unidad no aprobada se
        // trata como inexistente, para no confirmar su existencia.
        if (unit is null || unit.PublicationState != PublicationState.Aprobada)
        {
            return OperationResult<ConversationView>.NotFound();
        }

        if (unit.OwnerAccountId == requesterAccountId)
        {
            return OperationResult<ConversationView>.Forbidden();
        }

        var id = Conversation.ComputeId(unitId, unit.OwnerAccountId, requesterAccountId);
        var existing = await _conversations.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        var conversation = existing;

        if (conversation is null)
        {
            conversation = Conversation.Start(unitId, unit.OwnerAccountId, requesterAccountId, _clock.UtcNow);
            await _conversations.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);
        }

        var otherAccount = await _accounts
            .FindByIdAsync(unit.OwnerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (otherAccount is null)
        {
            return OperationResult<ConversationView>.NotFound();
        }

        return OperationResult<ConversationView>.Success(new ConversationView(
            conversation.Id,
            conversation.UnitId,
            otherAccount.Id,
            otherAccount.FullName,
            conversation.CreatedAt));
    }
}

public sealed record ConversationView(
    Guid ConversationId,
    Guid UnitId,
    Guid OtroAccountId,
    string OtroNombreCompleto,
    DateTimeOffset CreadaEn);
