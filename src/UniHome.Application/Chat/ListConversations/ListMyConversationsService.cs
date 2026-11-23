using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Units.Abstractions;

namespace UniHome.Application.Chat.ListConversations;

/// <summary>
/// EP-M08-03 (HU-069): conversaciones propias con nombre del otro participante,
/// último mensaje, hora y contexto mínimo de la unidad. Nunca correo ni
/// teléfono (HU-070): solo se proyecta <c>FullName</c> de la cuenta.
/// </summary>
public sealed class ListMyConversationsService
{
    private readonly IConversationRepository _conversations;
    private readonly IUnitRepository _units;
    private readonly IAccountRepository _accounts;

    public ListMyConversationsService(
        IConversationRepository conversations,
        IUnitRepository units,
        IAccountRepository accounts)
    {
        _conversations = conversations;
        _units = units;
        _accounts = accounts;
    }

    public async Task<OperationResult<IReadOnlyList<ConversationSummaryView>>> ListAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var conversations = await _conversations
            .ListByParticipantAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        var views = new List<ConversationSummaryView>(conversations.Count);

        foreach (var conversation in conversations)
        {
            var otherAccountId = conversation.OtherParticipant(accountId);
            var other = await _accounts.FindByIdAsync(otherAccountId, cancellationToken).ConfigureAwait(false);
            var unit = await _units.FindByIdAsync(conversation.UnitId, cancellationToken).ConfigureAwait(false);

            if (other is null || unit is null)
            {
                continue;
            }

            views.Add(new ConversationSummaryView(
                conversation.Id,
                conversation.UnitId,
                unit.Type.ToString(),
                unit.PricePerPlace,
                other.Id,
                other.FullName,
                conversation.LastMessagePreview,
                conversation.LastMessageAt ?? conversation.CreatedAt));
        }

        return OperationResult<IReadOnlyList<ConversationSummaryView>>.Success(
            views.OrderByDescending(view => view.UltimaActividad).ToArray());
    }
}

public sealed record ConversationSummaryView(
    Guid ConversationId,
    Guid UnitId,
    string TipoUnidad,
    decimal PrecioMensualPorLugarMxn,
    Guid OtroAccountId,
    string OtroNombreCompleto,
    string? UltimoMensaje,
    DateTimeOffset UltimaActividad);
