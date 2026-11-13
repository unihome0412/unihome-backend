using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Properties.Exceptions;
using UniHome.Domain.Properties;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryPropertyRepository : IPropertyRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Property> _properties = [];
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public IReadOnlyList<Property> All
    {
        get
        {
            lock (_gate)
            {
                return _properties.Values.ToArray();
            }
        }
    }

    public Task AddAsync(Property property, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var key = string.Join(
                '|',
                property.OwnerAccountId.ToString("N"),
                property.AddressNormalized,
                property.ColoniaNormalized,
                property.NameNormalized);

            if (!_keys.Add(key))
            {
                throw new DuplicatePropertyException();
            }

            _properties[property.Id] = property;
        }

        return Task.CompletedTask;
    }

    public Task<Property?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _properties.TryGetValue(id, out var property);
            return Task.FromResult(property);
        }
    }
}
