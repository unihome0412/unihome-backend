using UniHome.Domain.Properties;

namespace UniHome.Application.Properties.Abstractions;

public interface IPropertyRepository
{
    Task AddAsync(Property property, CancellationToken cancellationToken = default);

    Task<Property?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Property>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
