using UniHome.Application.Universities.Abstractions;

namespace UniHome.Application.Universities.ListUniversities;

/// <summary>
/// EP-M03-03 (HU-077): catálogo administrativo completo, incluidas las
/// universidades inactivas. La paginación es explícita y acotada.
/// </summary>
public sealed class ListUniversitiesService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly IUniversityRepository _universities;

    public ListUniversitiesService(IUniversityRepository universities)
    {
        _universities = universities;
    }

    public async Task<ListUniversitiesResult> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (page < 1)
        {
            errors["page"] = ["La página debe ser mayor o igual que 1."];
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] =
            [
                $"El tamaño de página debe estar entre 1 y {MaxPageSize}."
            ];
        }

        if (errors.Count > 0)
        {
            return ListUniversitiesResult.ValidationFailed(errors);
        }

        var result = await _universities
            .ListAsync(page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return ListUniversitiesResult.Listed(result);
    }
}
