using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Units.Photos;
using UniHome.Domain.Reparaciones;

namespace UniHome.Application.Reparaciones;

/// <summary>
/// EP-M11-01 (HU-062/063): crea un reporte de reparación de una unidad propia
/// o donde la cuenta tiene vínculo activo. La fotografía es opcional y
/// reutiliza el mismo almacenamiento (Cloudinary, DEC-04) y validación de
/// contenido (firma JPG/PNG, tamaño) que las fotos de unidad (HU-032).
/// </summary>
public sealed class CreateReparacionService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IReparacionRepository _reparaciones;
    private readonly IUnitPhotoStorage _storage;
    private readonly ISystemClock _clock;

    public CreateReparacionService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IReparacionRepository reparaciones,
        IUnitPhotoStorage storage,
        ISystemClock clock)
    {
        _units = units;
        _links = links;
        _reparaciones = reparaciones;
        _storage = storage;
        _clock = clock;
    }

    public async Task<OperationResult<Reparacion>> CreateAsync(
        Guid reporterAccountId,
        Guid unitId,
        string? title,
        string? description,
        UnitPhotoUpload? photoUpload,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Reparacion>.NotFound();
        }

        if (!await HasUnitAccessAsync(reporterAccountId, unit.OwnerAccountId, unitId, cancellationToken)
                .ConfigureAwait(false))
        {
            return OperationResult<Reparacion>.Forbidden();
        }

        ReparacionFoto? photo = null;
        string? storageKey = null;

        if (photoUpload is not null)
        {
            ValidatedUnitPhoto validated;

            try
            {
                validated = UnitPhotoValidator.Validate(photoUpload);
            }
            catch (ArgumentException exception)
            {
                return OperationResult<Reparacion>.ValidationFailed("foto", exception.Message);
            }

            try
            {
                await using var content = new MemoryStream(validated.Content, writable: false);
                var stored = await _storage
                    .SaveAsync(content, validated.ContentType, cancellationToken)
                    .ConfigureAwait(false);
                storageKey = stored.StorageKey;
                photo = new ReparacionFoto(
                    stored.StorageKey, stored.ContentType, stored.SizeBytes, stored.Sha256);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (exception is StorageUnavailableException)
                {
                    throw;
                }

                throw new StorageUnavailableException();
            }
        }

        Reparacion reparacion;

        try
        {
            reparacion = Reparacion.Create(
                unitId, reporterAccountId, title, description, photo, _clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            if (storageKey is not null)
            {
                await CleanupAsync(storageKey, CancellationToken.None).ConfigureAwait(false);
            }

            var field = exception.ParamName == nameof(description) ? "descripcion" : "titulo";
            return OperationResult<Reparacion>.ValidationFailed(field, exception.Message);
        }

        try
        {
            await _reparaciones.AddAsync(reparacion, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (storageKey is not null)
        {
            await CleanupAsync(storageKey, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return OperationResult<Reparacion>.Success(reparacion);
    }

    private async Task<bool> HasUnitAccessAsync(
        Guid accountId,
        Guid ownerAccountId,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (accountId == ownerAccountId)
        {
            return true;
        }

        var link = await _links.FindAsync(accountId, unitId, cancellationToken).ConfigureAwait(false);
        return link is not null && link.Status == Domain.OccupancyLinks.OccupancyStatus.Activo;
    }

    private async Task CleanupAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // La limpieza no debe ocultar el error original.
        }
    }
}
