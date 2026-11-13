using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Exploration;

/// <summary>
/// Resuelve URLs de lectura solo si el proveedor DEC-04 está disponible. Sin
/// proveedor devuelve null/vacío en las vistas públicas; nunca simula un blob.
/// </summary>
public sealed class UnitPhotoUrlResolver
{
    private readonly IUnitPhotoStorage _storage;

    public UnitPhotoUrlResolver(IUnitPhotoStorage storage)
    {
        _storage = storage;
    }

    public string? Primary(Unit unit)
    {
        UnitPhoto? primary = null;

        for (var index = 0; index < unit.Photos.Count; index++)
        {
            if (unit.Photos[index].IsPrimary)
            {
                primary = unit.Photos[index];
                break;
            }
        }

        if (primary is null && unit.Photos.Count > 0)
        {
            primary = unit.Photos[0];
        }

        return primary is null ? null : TryResolve(primary.StorageKey);
    }

    public IReadOnlyList<string> Gallery(Unit unit)
    {
        var urls = new List<string>(unit.Photos.Count);

        foreach (var photo in unit.Photos.OrderBy(photo => photo.Order))
        {
            var url = TryResolve(photo.StorageKey);

            if (url is not null)
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    private string? TryResolve(string storageKey)
    {
        try
        {
            return _storage.GetReadUrl(storageKey);
        }
        catch (StorageUnavailableException)
        {
            return null;
        }
    }
}
