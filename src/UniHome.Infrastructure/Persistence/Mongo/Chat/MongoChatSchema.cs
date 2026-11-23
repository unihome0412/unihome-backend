namespace UniHome.Infrastructure.Persistence.Mongo.Chat;

/// <summary>Nombres físicos M08. Un cambio exige una migración nueva.</summary>
public static class MongoChatSchema
{
    public const string ConversationsCollection = "conversaciones";
    public const string MessagesCollection = "mensajes";

    public const string ConversationUnitField = "unitId";
    public const string ConversationOwnerField = "propietarioAccountId";
    public const string ConversationRequesterField = "solicitanteAccountId";
    public const string ConversationParticipantsField = "participantes";
    public const string ConversationCreatedAtField = "createdAt";
    public const string ConversationLastMessageAtField = "ultimoMensajeFecha";
    public const string ConversationLastMessagePreviewField = "ultimoMensajeTexto";

    public const string MessageConversationField = "conversacionId";
    public const string MessageSenderField = "remitenteAccountId";
    public const string MessageBodyField = "texto";
    public const string MessageSentAtField = "fecha";

    public const string ConversationParticipantsIndex = "ix_conversaciones_participantes";
    public const string MessageConversationDateIndex = "ix_mensajes_conversacion_fecha";
}
