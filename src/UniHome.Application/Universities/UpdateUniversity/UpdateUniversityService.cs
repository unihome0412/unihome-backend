using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Universities.Exceptions;
using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.UpdateUniversity;

/// <summary>
/// EP-M03-04 (HU-077): edita nombre y campus con la misma normalización del
/// alta. La escritura usa el nombre normalizado anterior como condición para que
/// dos ediciones concurrentes no se pisen silenciosamente.
/// </summary>
public sealed class UpdateUniversityService
{
    private readonly IUniversityRepository _universities;

    public UpdateUniversityService(IUniversityRepository universities)
    {
        _universities = universities;
    }

    public async Task<UpdateUniversityResult> UpdateAsync(
        Guid id,
        string? nameAndCampus,
        CancellationToken cancellationToken = default)
    {
        var university = await _universities
            .FindByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (university is null)
        {
            return UpdateUniversityResult.NotFound();
        }

        var expectedNormalizedName = university.NormalizedName;

        try
        {
            university.Rename(nameAndCampus);
        }
        catch (ArgumentException exception)
        {
            return UpdateUniversityResult.ValidationFailed(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["nombreYCampus"] = [exception.Message]
                });
        }

        try
        {
            var updated = await _universities
                .UpdateNameAndCampusAsync(
                    university,
                    expectedNormalizedName,
                    cancellationToken)
                .ConfigureAwait(false);

            return updated
                ? UpdateUniversityResult.Updated(university)
                : UpdateUniversityResult.Conflict();
        }
        catch (DuplicateUniversityException)
        {
            return UpdateUniversityResult.Duplicate();
        }
    }
}
