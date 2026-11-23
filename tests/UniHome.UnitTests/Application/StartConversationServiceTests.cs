using UniHome.Application.Chat.StartConversation;
using UniHome.Application.Common;
using UniHome.Domain.Properties;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class StartConversationServiceTests
{
    private static readonly DateTimeOffset Now = ExplorationTestData.Now;

    [Fact]
    public async Task StartAsync_UnidadAprobada_CreaConversacionConElPropietario()
    {
        var owner = AccountTestData.Create("owner-chat1@uni.edu");
        var requester = AccountTestData.Create("requester-chat1@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa Chat 1", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 3500);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var conversations = new FakeConversationRepository();
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requester);
        var service = new StartConversationService(units, conversations, accounts, new FixedClock(Now));

        var result = await service.StartAsync(requester.Id, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(unit.Id, result.Value!.UnitId);
        Assert.Equal(owner.Id, result.Value.OtroAccountId);
        Assert.Equal(owner.FullName, result.Value.OtroNombreCompleto);
        Assert.Single(conversations.Items);
    }

    [Fact]
    public async Task StartAsync_LlamadoDosVeces_ReutilizaLaMismaConversacion()
    {
        var owner = AccountTestData.Create("owner-chat2@uni.edu");
        var requester = AccountTestData.Create("requester-chat2@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa Chat 2", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 3500);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var conversations = new FakeConversationRepository();
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requester);
        var service = new StartConversationService(units, conversations, accounts, new FixedClock(Now));

        var first = await service.StartAsync(requester.Id, unit.Id);
        var second = await service.StartAsync(requester.Id, unit.Id);

        Assert.Equal(first.Value!.ConversationId, second.Value!.ConversationId);
        Assert.Single(conversations.Items);
    }

    [Fact]
    public async Task StartAsync_PropietarioConSuPropiaUnidad_DebeDevolverForbidden()
    {
        var owner = AccountTestData.Create("owner-chat3@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa Chat 3", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 3500);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var conversations = new FakeConversationRepository();
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(owner);
        var service = new StartConversationService(units, conversations, accounts, new FixedClock(Now));

        var result = await service.StartAsync(owner.Id, unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
        Assert.Empty(conversations.Items);
    }

    [Fact]
    public async Task StartAsync_UnidadNoAprobada_DebeDevolverNotFound()
    {
        var owner = AccountTestData.Create("owner-chat4@uni.edu");
        var requester = AccountTestData.Create("requester-chat4@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa Chat 4", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(
            owner.Id, property.Id, 3500, state: PublicationState.EnRevision);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var conversations = new FakeConversationRepository();
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requester);
        var service = new StartConversationService(units, conversations, accounts, new FixedClock(Now));

        var result = await service.StartAsync(requester.Id, unit.Id);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task StartAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var requester = AccountTestData.Create("requester-chat5@uni.edu");
        var units = new FakeUnitRepository();
        var conversations = new FakeConversationRepository();
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(requester);
        var service = new StartConversationService(units, conversations, accounts, new FixedClock(Now));

        var result = await service.StartAsync(requester.Id, Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
