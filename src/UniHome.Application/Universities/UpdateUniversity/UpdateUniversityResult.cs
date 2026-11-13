using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.UpdateUniversity;

public enum UpdateUniversityStatus
{
    Updated,
    ValidationFailed,
    NotFound,
    Duplicate,
    Conflict
}

public sealed record UpdateUniversityResult(
    UpdateUniversityStatus Status,
    University? University,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static UpdateUniversityResult Updated(University university) =>
        new(UpdateUniversityStatus.Updated, university, EmptyErrors);

    public static UpdateUniversityResult NotFound() =>
        new(UpdateUniversityStatus.NotFound, null, EmptyErrors);

    public static UpdateUniversityResult Duplicate() =>
        new(UpdateUniversityStatus.Duplicate, null, EmptyErrors);

    public static UpdateUniversityResult Conflict() =>
        new(UpdateUniversityStatus.Conflict, null, EmptyErrors);

    public static UpdateUniversityResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(UpdateUniversityStatus.ValidationFailed, null, errors);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
