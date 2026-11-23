using UniHome.Application.Chat.ListMessages;
using UniHome.Application.Common;
using UniHome.Domain.Chat;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ListConversationMessagesServiceTests
{
    private static readonly DateTimeOffset Now = ExplorationTestData.Now;

    private static (FakeConversationRepository Conversations, Conversation Conversation) SeedConversation()
    {
        var conversations = new FakeConversationRepository();
        var conversation = Conversation.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        conversations.Items.Add(conversation);
        return (conversations, conversation);
    }

    [Fact]
    public async Task ListAsync_Participante_DevuelveMasRecientesPrimero()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        messages.Items.Add(Message.Create(conversation.Id, conversation.OwnerAccountId, "Uno", Now));
        messages.Items.Add(Message.Create(conversation.Id, conversation.RequesterAccountId, "Dos", Now.AddMinutes(1)));
        var service = new ListConversationMessagesService(conversations, messages);

        var result = await service.ListAsync(conversation.OwnerAccountId, conversation.Id, before: null, pageSize: null);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Mensajes.Count);
        Assert.Equal("Dos", result.Value.Mensajes[0].Texto);
        Assert.Equal("Uno", result.Value.Mensajes[1].Texto);
        Assert.Null(result.Value.SiguienteCursor);
    }

    [Fact]
    public async Task ListAsync_PaginaCompleta_DevuelveCursorParaSeguirHaciaAtras()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        for (var i = 0; i < 3; i++)
        {
            messages.Items.Add(Message.Create(
                conversation.Id, conversation.OwnerAccountId, $"Mensaje {i}", Now.AddMinutes(i)));
        }

        var service = new ListConversationMessagesService(conversations, messages);

        var result = await service.ListAsync(conversation.OwnerAccountId, conversation.Id, before: null, pageSize: 2);

        Assert.Equal(2, result.Value!.Mensajes.Count);
        Assert.NotNull(result.Value.SiguienteCursor);

        var nextPage = await service.ListAsync(
            conversation.OwnerAccountId, conversation.Id, before: result.Value.SiguienteCursor, pageSize: 2);

        Assert.Single(nextPage.Value!.Mensajes);
        Assert.Null(nextPage.Value.SiguienteCursor);
    }

    [Fact]
    public async Task ListAsync_NoParticipante_DebeDevolverForbidden()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new ListConversationMessagesService(conversations, messages);

        var result = await service.ListAsync(Guid.NewGuid(), conversation.Id, before: null, pageSize: null);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ListAsync_ConversacionInexistente_DebeDevolverNotFound()
    {
        var conversations = new FakeConversationRepository();
        var messages = new FakeMessageRepository();
        var service = new ListConversationMessagesService(conversations, messages);

        var result = await service.ListAsync(Guid.NewGuid(), Guid.NewGuid(), before: null, pageSize: null);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
