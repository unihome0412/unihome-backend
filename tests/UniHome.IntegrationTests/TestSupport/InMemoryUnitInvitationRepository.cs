using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.OccupancyLinks.Exceptions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryUnitInvitationRepository : IUnitInvitationRepository
{
    private readonly object _gate = new();
    private readonly List<UnitInvitation> _invitations = [];

    public Task<UnitInvitation?> FindLatestByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var latest = _invitations
                .Where(invitation => invitation.UnitId == unitId)
                .OrderByDescending(invitation => invitation.CreatedAt)
                .FirstOrDefault();
            return Task.FromResult(latest);
        }
    }

    public Task<UnitInvitation?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var invitation = _invitations.SingleOrDefault(
                item => string.Equals(item.Code, code, StringComparison.Ordinal));
            return Task.FromResult(invitation);
        }
    }

    public Task AddAsync(UnitInvitation invitation, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_invitations.Any(item => string.Equals(
                    item.Code,
                    invitation.Code,
                    StringComparison.Ordinal)))
            {
                throw new DuplicateInvitationCodeException();
            }

            _invitations.Add(invitation);
        }

        return Task.CompletedTask;
    }
}
