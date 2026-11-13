using UniHome.Application.Universities.Abstractions;

namespace UniHome.Application.Universities.ListUniversities;

public sealed record ListUniversitiesResult(
    UniversityPage? Page,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static ListUniversitiesResult Listed(UniversityPage page) =>
        new(page, EmptyErrors);

    public static ListUniversitiesResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(null, errors);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
