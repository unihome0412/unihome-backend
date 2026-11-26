using UniHome.Application.Abstractions.Caching;
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
    private readonly IPublicCacheInvalidator _publicCache;

    public UpdateUniversityService(IUniversityRepository universities, IPublicCacheInvalidator publicCache)
    {
        _universities = universities;
        _publicCache = publicCache;
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

            if (updated)
            {
                await _publicCache
                    .EvictAsync(PublicCacheTags.Universities, CancellationToken.None)
                    .ConfigureAwait(false);
                return UpdateUniversityResult.Updated(university);
            }

            return UpdateUniversityResult.Conflict();
        }
        catch (DuplicateUniversityException)
        {
            return UpdateUniversityResult.Duplicate();
        }
    }
}
