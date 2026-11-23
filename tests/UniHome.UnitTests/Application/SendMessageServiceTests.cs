using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Chat.SendMessage;
using UniHome.Application.Common;
using UniHome.Domain.Chat;
using UniHome.Infrastructure.Redis;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class SendMessageServiceTests
{
    private static readonly DateTimeOffset Now = ExplorationTestData.Now;

    private sealed class AlwaysUnavailableRateLimiter : IDistributedRateLimiter
    {
        public Task<RateLimiterReadiness> CheckReadinessAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(RateLimiterReadiness.Unavailable());

        public Task<RateLimitResult> CheckAndIncrementAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RateLimitResult.ServiceUnavailable("no disponible"));
    }

    private static (FakeConversationRepository Conversations, Conversation Conversation) SeedConversation()
    {
        var conversations = new FakeConversationRepository();
        var conversation = Conversation.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        conversations.Items.Add(conversation);
        return (conversations, conversation);
    }

    [Fact]
    public async Task SendAsync_ParticipanteConTextoValido_GuardaMensajeYActualizaResumen()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new InMemoryDistributedRateLimiter(new FixedClock(Now)), new FixedClock(Now));

        var result = await service.SendAsync(conversation.OwnerAccountId, conversation.Id, "Hola, ¿sigue disponible?");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Hola, ¿sigue disponible?", result.Value!.Texto);
        Assert.Single(messages.Items);
        Assert.Equal("Hola, ¿sigue disponible?", conversation.LastMessagePreview);
        Assert.Equal(Now, conversation.LastMessageAt);
    }

    [Fact]
    public async Task SendAsync_NoParticipante_DebeDevolverForbidden()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new InMemoryDistributedRateLimiter(new FixedClock(Now)), new FixedClock(Now));

        var result = await service.SendAsync(Guid.NewGuid(), conversation.Id, "Hola");

        Assert.Equal(OperationStatus.Forbidden, result.Status);
        Assert.Empty(messages.Items);
    }

    [Fact]
    public async Task SendAsync_ConversacionInexistente_DebeDevolverNotFound()
    {
        var conversations = new FakeConversationRepository();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new InMemoryDistributedRateLimiter(new FixedClock(Now)), new FixedClock(Now));

        var result = await service.SendAsync(Guid.NewGuid(), Guid.NewGuid(), "Hola");

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SendAsync_TextoVacio_DebeDevolverValidationFailed()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new InMemoryDistributedRateLimiter(new FixedClock(Now)), new FixedClock(Now));

        var result = await service.SendAsync(conversation.OwnerAccountId, conversation.Id, "   ");

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Contains("texto", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task SendAsync_TextoExcedeLongitud_DebeDevolverValidationFailed()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new InMemoryDistributedRateLimiter(new FixedClock(Now)), new FixedClock(Now));
        var tooLong = new string('a', Message.MaxBodyLength + 1);

        var result = await service.SendAsync(conversation.OwnerAccountId, conversation.Id, tooLong);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Empty(messages.Items);
    }

    [Fact]
    public async Task SendAsync_ExcedeElLimiteDeFrecuencia_DebeDevolverRateLimited()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new InMemoryDistributedRateLimiter(new FixedClock(Now)), new FixedClock(Now));

        for (var i = 0; i < SendMessageService.MessagesPerWindow; i++)
        {
            var allowed = await service.SendAsync(conversation.OwnerAccountId, conversation.Id, $"Mensaje {i}");
            Assert.Equal(OperationStatus.Success, allowed.Status);
        }

        var result = await service.SendAsync(conversation.OwnerAccountId, conversation.Id, "Uno más");

        Assert.Equal(OperationStatus.RateLimited, result.Status);
        Assert.Equal(SendMessageService.MessagesPerWindow, messages.Items.Count);
    }

    [Fact]
    public async Task SendAsync_AlmacenDeLimiteNoDisponible_PermiteElEnvio()
    {
        var (conversations, conversation) = SeedConversation();
        var messages = new FakeMessageRepository();
        var service = new SendMessageService(
            conversations, messages, new AlwaysUnavailableRateLimiter(), new FixedClock(Now));

        var result = await service.SendAsync(conversation.OwnerAccountId, conversation.Id, "Hola");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Single(messages.Items);
    }
}
