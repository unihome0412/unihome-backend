using UniHome.Application.Universities.Abstractions;

namespace UniHome.Application.Universities.ChangeUniversityStatus;

/// <summary>
/// EP-M03-05 (HU-077): activa o desactiva sin borrado físico. La búsqueda
/// pública solo devuelve activas; el catálogo administrativo conserva ambas.
/// </summary>
public sealed class ChangeUniversityStatusService
{
    private readonly IUniversityRepository _universities;

    public ChangeUniversityStatusService(IUniversityRepository universities)
    {
        _universities = universities;
    }

    public async Task<ChangeUniversityStatusResult> ChangeAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var university = await _universities
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (university is null)
        {
            return ChangeUniversityStatusResult.NotFound();
        }

        if (university.IsActive != isActive)
        {
            var updated = await _universities
                .SetActiveAsync(id, isActive, cancellationToken)
                .ConfigureAwait(false);

            if (!updated)
            {
                return ChangeUniversityStatusResult.NotFound();
            }
        }

        university.SetActive(isActive);
        return ChangeUniversityStatusResult.Updated(university);
    }
}
