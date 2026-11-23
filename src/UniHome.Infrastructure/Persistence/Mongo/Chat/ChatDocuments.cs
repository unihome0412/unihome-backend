using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Chat;

/// <summary>
/// <c>_id</c> determinista (<c>Conversation.ComputeId</c>) garantiza unicidad
/// nativa sin depender de un índice declarado, igual que
/// <c>OccupancyLinkDocument</c>. <c>participantes</c> es el único campo con
/// índice observado en Atlas (<c>ix_conversaciones_participantes</c>).
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ConversationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.ConversationUnitField)]
    public string UnitId { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.ConversationOwnerField)]
    public string OwnerAccountId { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.ConversationRequesterField)]
    public string RequesterAccountId { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.ConversationParticipantsField)]
    public List<string> Participants { get; set; } = [];

    [BsonElement(MongoChatSchema.ConversationCreatedAtField)]
    public DateTime CreatedAt { get; set; }

    [BsonElement(MongoChatSchema.ConversationLastMessageAtField)]
    [BsonIgnoreIfNull]
    public DateTime? LastMessageAt { get; set; }

    [BsonElement(MongoChatSchema.ConversationLastMessagePreviewField)]
    [BsonIgnoreIfNull]
    public string? LastMessagePreview { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MessageDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.MessageConversationField)]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.MessageSenderField)]
    public string SenderAccountId { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.MessageBodyField)]
    public string Body { get; set; } = string.Empty;

    [BsonElement(MongoChatSchema.MessageSentAtField)]
    public DateTime SentAt { get; set; }
}
