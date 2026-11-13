using UniHome.Application.Common;
using UniHome.Application.Publications.AcceptTerms;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class AcceptTermsServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Accept_VersionValida_DebeRegistrarAceptacion()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var repository = new FakeTermsAcceptanceRepository();
        var service = new AcceptTermsService(repository, eligibility, new FixedClock(Now));

        var result = await service.AcceptAsync(
            account.Id,
            new AcceptTermsCommand("2026-11"));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Single(repository.Items);
        Assert.Equal("2026-11", repository.Items[0].Version);
    }

    [Fact]
    public async Task Accept_VersionRepetida_DebeDevolverConflict()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var repository = new FakeTermsAcceptanceRepository { ExistsResult = true };
        var service = new AcceptTermsService(repository, eligibility, new FixedClock(Now));

        var result = await service.AcceptAsync(
            account.Id,
            new AcceptTermsCommand("2026-11"));

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Accept_MenorDeEdad_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Minor();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var service = new AcceptTermsService(
            new FakeTermsAcceptanceRepository(),
            eligibility,
            new FixedClock(Now));

        var result = await service.AcceptAsync(
            account.Id,
            new AcceptTermsCommand("2026-11"));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
