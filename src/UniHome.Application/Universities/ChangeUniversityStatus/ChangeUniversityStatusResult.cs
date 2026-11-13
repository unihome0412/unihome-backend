using UniHome.Domain.Universities;

namespace UniHome.Application.Universities.ChangeUniversityStatus;

public enum ChangeUniversityStatus
{
    Updated,
    NotFound
}

public sealed record ChangeUniversityStatusResult(
    ChangeUniversityStatus Status,
    University? University)
{
    public static ChangeUniversityStatusResult Updated(University university) =>
        new(ChangeUniversityStatus.Updated, university);

    public static ChangeUniversityStatusResult NotFound() =>
        new(ChangeUniversityStatus.NotFound, null);
}
