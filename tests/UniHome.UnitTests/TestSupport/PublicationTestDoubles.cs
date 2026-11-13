using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Publications;
using UniHome.Domain.Units;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakePropertyRepository : IPropertyRepository
{
    public List<Property> Items { get; } = [];

    public Exception? AddException { get; set; }

    public Task AddAsync(Property property, CancellationToken cancellationToken = default)
    {
        if (AddException is not null)
        {
            throw AddException;
        }

        Items.Add(property);
        return Task.CompletedTask;
    }

    public Task<Property?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(property => property.Id == id));
}

internal sealed class FakeUnitRepository : IUnitRepository
{
    public List<Unit> Items { get; } = [];

    public bool UpdateResult { get; set; } = true;

    public bool AppendResult { get; set; } = true;

    public Task AddAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        Items.Add(unit);
        return Task.CompletedTask;
    }

    public Task<Unit?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(unit => unit.Id == id));

    public Task<bool> UpdateServicesAsync(
        Guid id,
        Guid propertyId,
        IReadOnlyList<ServiceCharge> services,
        CancellationToken cancellationToken = default)
    {
        if (UpdateResult)
        {
            Items.SingleOrDefault(unit => unit.Id == id)?.ConfigureServices(services);
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<bool> UpdateZoneRulesAsync(
        Guid id,
        Guid propertyId,
        string? zoneInfo,
        string? rules,
        CancellationToken cancellationToken = default)
    {
        if (UpdateResult)
        {
            Items.SingleOrDefault(unit => unit.Id == id)?.ConfigureZoneRules(zoneInfo, rules);
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<bool> TryAppendPhotosAsync(
        Guid id,
        Guid propertyId,
        int expectedPhotoCount,
        IReadOnlyList<UnitPhoto> photos,
        CancellationToken cancellationToken = default)
    {
        if (!AppendResult)
        {
            return Task.FromResult(false);
        }

        var unit = Items.SingleOrDefault(item => item.Id == id);

        if (unit is null || unit.Photos.Count != expectedPhotoCount)
        {
            return Task.FromResult(false);
        }

        unit.AppendPhotos(photos);
        return Task.FromResult(true);
    }
}

internal sealed class FakeTermsAcceptanceRepository : ITermsAcceptanceRepository
{
    public List<TermsAcceptance> Items { get; } = [];

    public bool ExistsResult { get; set; }

    public Task AddAsync(
        TermsAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        Items.Add(acceptance);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid accountId,
        string version,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistsResult || Items.Any(item =>
            item.AccountId == accountId &&
            string.Equals(item.Version, version, StringComparison.Ordinal)));
}

internal sealed class FakeUnitPhotoStorage : IUnitPhotoStorage
{
    public List<string> Saved { get; } = [];
    public List<string> Deleted { get; } = [];
    public bool ThrowOnSave { get; set; }

    public Task<StoredUnitPhoto> SaveAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave)
        {
            throw new StorageUnavailableException();
        }

        var key = $"photo-{Saved.Count + 1}";
        Saved.Add(key);
        return Task.FromResult(new StoredUnitPhoto(
            key,
            contentType,
            content.Length,
            "hash"));
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        Deleted.Add(storageKey);
        return Task.CompletedTask;
    }

    public string GetReadUrl(string storageKey) => $"https://photos.test/{storageKey}";
}
