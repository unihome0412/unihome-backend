using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.CreateUniversity;

public enum CreateUniversityStatus
{
    Created,
    ValidationFailed,
    Duplicate
}

public sealed record CreateUniversityResult(
    CreateUniversityStatus Status,
    University? University,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static CreateUniversityResult Created(University university) =>
        new(CreateUniversityStatus.Created, university, EmptyErrors);

    public static CreateUniversityResult Duplicate() =>
        new(CreateUniversityStatus.Duplicate, null, EmptyErrors);

    public static CreateUniversityResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(CreateUniversityStatus.ValidationFailed, null, errors);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
