using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeOccupancyLinkRepository : IOccupancyLinkRepository
{
    public List<OccupancyLink> Items { get; } = [];

    public Task<OccupancyLink?> FindAsync(
        Guid tenantAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(
            link => link.TenantAccountId == tenantAccountId && link.UnitId == unitId));

    public Task SaveAsync(OccupancyLink link, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(item =>
            item.TenantAccountId == link.TenantAccountId && item.UnitId == link.UnitId);
        Items.Add(link);
        return Task.CompletedTask;
    }

    public Task<int> CountActiveAsync(Guid unitId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(
            link => link.UnitId == unitId && link.Status == OccupancyStatus.Activo));

    public Task<bool> HasActiveLinkAsync(
        Guid tenantAccountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(
            link => link.TenantAccountId == tenantAccountId && link.Status == OccupancyStatus.Activo));
}

internal sealed class FakeUnitInvitationRepository : IUnitInvitationRepository
{
    public List<UnitInvitation> Items { get; } = [];

    public Task<UnitInvitation?> FindLatestByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items
            .Where(invitation => invitation.UnitId == unitId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .FirstOrDefault());

    public Task<UnitInvitation?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(
            invitation => string.Equals(invitation.Code, code, StringComparison.Ordinal)));

    public Task AddAsync(UnitInvitation invitation, CancellationToken cancellationToken = default)
    {
        if (Items.Any(item => string.Equals(item.Code, invitation.Code, StringComparison.Ordinal)))
        {
            throw new UniHome.Application.OccupancyLinks.Exceptions.DuplicateInvitationCodeException();
        }

        Items.Add(invitation);
        return Task.CompletedTask;
    }
}

internal sealed class SequentialUnitInvitationCodeGenerator : IUnitInvitationCodeGenerator
{
    private readonly Queue<string> _codes;
    private int _counter;

    public SequentialUnitInvitationCodeGenerator(params string[] codes)
    {
        _codes = new Queue<string>(codes);
    }

    public string Generate() =>
        _codes.Count > 0 ? _codes.Dequeue() : $"CODE{++_counter:D4}";
}
