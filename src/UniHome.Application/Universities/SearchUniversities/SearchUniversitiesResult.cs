using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.SearchUniversities;

public sealed record SearchUniversitiesResult(
    IReadOnlyList<University> Universities,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static SearchUniversitiesResult Found(IReadOnlyList<University> universities) =>
        new(universities, EmptyErrors);

    public static SearchUniversitiesResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new([], errors);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
