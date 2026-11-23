using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Chat;
using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Chat.ListConversations;
using UniHome.Application.Chat.ListMessages;
using UniHome.Application.Chat.SendMessage;
using UniHome.Application.Chat.StartConversation;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M08 (HU-067/068/069/070/086/089): conversaciones y mensajes entre el
/// propietario de una unidad y quien la contacta desde su ficha. Todos los
/// endpoints exigen sesión (HU-086) y ninguna respuesta expone correo ni
/// teléfono (HU-070).
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class ChatController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly StartConversationService _startConversation;
    private readonly SendMessageService _sendMessage;
    private readonly ListConversationMessagesService _listMessages;
    private readonly ListMyConversationsService _listConversations;

    public ChatController(
        StartConversationService startConversation,
        SendMessageService sendMessage,
        ListConversationMessagesService listMessages,
        ListMyConversationsService listConversations)
    {
        _startConversation = startConversation;
        _sendMessage = sendMessage;
        _listMessages = listMessages;
        _listConversations = listConversations;
    }

    /// <summary>EP-M08-01 (HU-067): inicia o reutiliza la conversación con el propietario de la unidad.</summary>
    [HttpPost("unidades/{unitId:guid}/conversaciones")]
    [Authorize]
    [ProducesResponseType(typeof(ConversacionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartConversation(Guid unitId, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _startConversation
            .StartAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(ConversacionResponse.FromView(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "No puedes iniciar una conversación con tu propia unidad.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M08-03 (HU-069): conversaciones propias con último mensaje y contexto de la unidad.</summary>
    [HttpGet("conversaciones")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ConversacionResumenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListConversations(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _listConversations.ListAsync(accountId, cancellationToken).ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(value.Select(ConversacionResumenResponse.FromView).ToArray()),
            notFoundDetail: "No se pudo completar la operación.",
            forbiddenDetail: "No se pudo completar la operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M08-04 (HU-068): historial paginado, solo para participantes.</summary>
    [HttpGet("conversaciones/{conversationId:guid}/mensajes")]
    [Authorize]
    [ProducesResponseType(typeof(PaginaMensajesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListMessages(
        Guid conversationId,
        [FromQuery(Name = "antes")] DateTimeOffset? antes,
        [FromQuery(Name = "antesId")] Guid? antesId,
        [FromQuery(Name = "limite")] int? limite,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        // El cursor es compuesto (fecha, id): sin ambos, se trata como la
        // primera página en vez de aplicar un corte parcial ambiguo.
        var before = antes is not null && antesId is not null
            ? new MessageCursor(antes.Value, antesId.Value)
            : null;

        var result = await _listMessages
            .ListAsync(accountId, conversationId, before, limite, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(PaginaMensajesResponse.FromView(value)),
            notFoundDetail: "La conversación no existe.",
            forbiddenDetail: "No eres parte de esta conversación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M08-02 (HU-068/HU-089): envía un mensaje; solo participantes, con límite de longitud y frecuencia.</summary>
    [HttpPost("conversaciones/{conversationId:guid}/mensajes")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(MensajeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] EnviarMensajeRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var result = await _sendMessage
            .SendAsync(accountId, conversationId, request.Texto, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => StatusCode(StatusCodes.Status201Created, MensajeResponse.FromView(value)),
            notFoundDetail: "La conversación no existe.",
            forbiddenDetail: "No eres parte de esta conversación.",
            conflictDetail: "No se pudo completar la operación.",
            rateLimitedDetail: "Enviaste demasiados mensajes. Intenta de nuevo en un minuto.");
    }
}
