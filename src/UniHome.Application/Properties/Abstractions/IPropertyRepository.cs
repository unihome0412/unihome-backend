using UniHome.Domain.Properties;

namespace UniHome.Application.Properties.Abstractions;

public interface IPropertyRepository
{
    Task AddAsync(Property property, CancellationToken cancellationToken = default);

    Task<Property?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Property>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-M05-11 (HU-033): todas las propiedades del titular, ordenadas por
    /// fecha de alta. Usa el índice existente de propietario; no crea colección.
    /// </summary>
    Task<IReadOnlyList<Property>> ListByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default);
}
