using UniHome.Application.Chat.ListConversations;
using UniHome.Application.Common;
using UniHome.Domain.Chat;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ListMyConversationsServiceTests
{
    private static readonly DateTimeOffset Now = ExplorationTestData.Now;

    [Fact]
    public async Task ListAsync_DevuelveContextoDeLaUnidadYUltimoMensaje()
    {
        var owner = AccountTestData.Create("owner-list1@uni.edu");
        var requester = AccountTestData.Create("requester-list1@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa Lista 1", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 4200);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requester);
        var conversations = new FakeConversationRepository();
        var conversation = Conversation.Start(unit.Id, owner.Id, requester.Id, Now);
        conversation.RegisterMessage("¿Sigue disponible?", Now.AddMinutes(10));
        conversations.Items.Add(conversation);
        var service = new ListMyConversationsService(conversations, units, accounts);

        var result = await service.ListAsync(owner.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        var summary = Assert.Single(result.Value!);
        Assert.Equal(unit.Id, summary.UnitId);
        Assert.Equal(requester.Id, summary.OtroAccountId);
        Assert.Equal(requester.FullName, summary.OtroNombreCompleto);
        Assert.Equal("¿Sigue disponible?", summary.UltimoMensaje);
        Assert.Equal(Now.AddMinutes(10), summary.UltimaActividad);
    }

    [Fact]
    public async Task ListAsync_SinConversaciones_DevuelveListaVacia()
    {
        var units = new FakeUnitRepository();
        var accounts = new InMemoryAccountRepository();
        var conversations = new FakeConversationRepository();
        var service = new ListMyConversationsService(conversations, units, accounts);

        var result = await service.ListAsync(Guid.NewGuid());

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListAsync_OrdenaPorActividadMasReciente()
    {
        var owner = AccountTestData.Create("owner-list2@uni.edu");
        var requesterA = AccountTestData.Create("requester-list2a@uni.edu");
        var requesterB = AccountTestData.Create("requester-list2b@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa Lista 2", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 4200);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var accounts = new InMemoryAccountRepository();
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requesterA);
        await accounts.AddAsync(requesterB);
        var conversations = new FakeConversationRepository();
        var older = Conversation.Start(unit.Id, owner.Id, requesterA.Id, Now);
        older.RegisterMessage("Antiguo", Now.AddMinutes(1));
        var newer = Conversation.Start(unit.Id, owner.Id, requesterB.Id, Now);
        newer.RegisterMessage("Reciente", Now.AddMinutes(20));
        conversations.Items.Add(older);
        conversations.Items.Add(newer);
        var service = new ListMyConversationsService(conversations, units, accounts);

        var result = await service.ListAsync(owner.Id);

        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(requesterB.Id, result.Value[0].OtroAccountId);
        Assert.Equal(requesterA.Id, result.Value[1].OtroAccountId);
    }
}
