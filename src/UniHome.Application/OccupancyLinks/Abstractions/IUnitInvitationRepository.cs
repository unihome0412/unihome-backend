using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.Abstractions;

public interface IUnitInvitationRepository
{
    /// <summary>Invitación más reciente de la unidad, vigente o no.</summary>
    Task<UnitInvitation?> FindLatestByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<UnitInvitation?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task AddAsync(UnitInvitation invitation, CancellationToken cancellationToken = default);
}
