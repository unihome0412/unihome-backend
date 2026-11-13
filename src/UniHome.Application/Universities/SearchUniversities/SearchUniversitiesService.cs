using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.SearchUniversities;

/// <summary>
/// Consulta pública EP-M03-01: solo universidades activas, coincidencia sin
/// mayúsculas ni acentos y como máximo 10 resultados (HU-007/HU-075).
/// </summary>
public sealed class SearchUniversitiesService
{
    public const int MaxResults = 10;
    public const int MaxSearchLength = 100;

    private readonly IUniversityRepository _universities;

    public SearchUniversitiesService(IUniversityRepository universities)
    {
        _universities = universities;
    }

    public async Task<SearchUniversitiesResult> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return SearchUniversitiesResult.Found([]);
        }

        if (search.Trim().Length > MaxSearchLength)
        {
            return SearchUniversitiesResult.ValidationFailed(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["buscar"] =
                    [
                        $"El término de búsqueda no debe superar " +
                        $"{MaxSearchLength} caracteres."
                    ]
                });
        }

        var normalized = University.Normalize(search);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return SearchUniversitiesResult.Found([]);
        }

        var universities = await _universities
            .SearchActiveAsync(normalized, MaxResults, cancellationToken)
            .ConfigureAwait(false);

        return SearchUniversitiesResult.Found(universities);
    }
}
