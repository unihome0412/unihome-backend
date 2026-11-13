using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Units.Photos;

/// <summary>
/// EP-M05-03 (HU-032): valida y almacena hasta 10 fotos por unidad. El binario
/// vive en el proveedor externo (DEC-04) y MongoDB solo conserva metadatos.
/// </summary>
public sealed class AddUnitPhotosService
{
    private readonly IUnitRepository _units;
    private readonly IUnitPhotoStorage _storage;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public AddUnitPhotosService(
        IUnitRepository units,
        IUnitPhotoStorage storage,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _storage = storage;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<Unit>> AddAsync(
        Guid ownerAccountId,
        Guid unitId,
        IReadOnlyList<UnitPhotoUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uploads);

        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<Unit>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Unit>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Unit>.Forbidden();
        }

        if (uploads.Count == 0)
        {
            return OperationResult<Unit>.ValidationFailed(
                "fotos",
                "Agrega al menos una foto de la habitación.");
        }

        if (unit.Photos.Count + uploads.Count > Unit.MaxPhotos)
        {
            return OperationResult<Unit>.ValidationFailed(
                "fotos",
                $"Una unidad puede tener como máximo {Unit.MaxPhotos} fotografías.");
        }

        var validated = new List<ValidatedUnitPhoto>(uploads.Count);

        try
        {
            foreach (var upload in uploads)
            {
                validated.Add(UnitPhotoValidator.Validate(upload));
            }
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Unit>.ValidationFailed("fotos", exception.Message);
        }

        var stored = new List<StoredUnitPhoto>(validated.Count);

        try
        {
            foreach (var photo in validated)
            {
                await using var content = new MemoryStream(photo.Content, writable: false);
                stored.Add(await _storage
                    .SaveAsync(content, photo.ContentType, cancellationToken)
                    .ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException)
        {
            await CleanupAsync(stored, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await CleanupAsync(stored, CancellationToken.None).ConfigureAwait(false);

            if (exception is StorageUnavailableException)
            {
                throw;
            }

            throw new StorageUnavailableException();
        }

        var expectedPhotoCount = unit.Photos.Count;
        var photos = validated
            .Select((photo, index) => new UnitPhoto(
                stored[index].StorageKey,
                stored[index].ContentType,
                stored[index].SizeBytes,
                stored[index].Sha256,
                expectedPhotoCount + index,
                expectedPhotoCount == 0 && index == 0,
                _clock.UtcNow))
            .ToArray();

        bool appended;

        try
        {
            appended = await _units
                .TryAppendPhotosAsync(
                    unit.Id,
                    unit.PropertyId,
                    expectedPhotoCount,
                    photos,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await CleanupAsync(stored, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await CleanupAsync(stored, CancellationToken.None).ConfigureAwait(false);

            if (exception is StorageUnavailableException)
            {
                throw;
            }

            throw new StorageUnavailableException();
        }

        if (!appended)
        {
            await CleanupAsync(stored, CancellationToken.None).ConfigureAwait(false);
            return OperationResult<Unit>.Conflict();
        }

        var updated = await _units.FindByIdAsync(unit.Id, cancellationToken).ConfigureAwait(false);
        return updated is null
            ? OperationResult<Unit>.NotFound()
            : OperationResult<Unit>.Success(updated);
    }

    private async Task CleanupAsync(
        IReadOnlyList<StoredUnitPhoto> stored,
        CancellationToken cancellationToken)
    {
        foreach (var photo in stored)
        {
            try
            {
                await _storage
                    .DeleteAsync(photo.StorageKey, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // La limpieza no debe ocultar el error original ni registrar claves.
            }
        }
    }
}
