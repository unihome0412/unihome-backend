using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Chat;

internal static class MongoChatIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoChatSchema.ConversationsCollection,
            [
                new(
                    MongoChatSchema.ConversationParticipantsIndex,
                    [new(MongoChatSchema.ConversationParticipantsField, 1)])
            ]),
        new(
            MongoChatSchema.MessagesCollection,
            [
                new(
                    MongoChatSchema.MessageConversationDateIndex,
                    [
                        new(MongoChatSchema.MessageConversationField, 1),
                        new(MongoChatSchema.MessageSentAtField, -1)
                    ])
            ])
    ];
}
