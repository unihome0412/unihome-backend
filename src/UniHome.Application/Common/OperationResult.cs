namespace UniHome.Application.Common;

public enum OperationStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Forbidden,
    Conflict
}

public sealed record OperationResult<T>(
    OperationStatus Status,
    T? Value,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static OperationResult<T> Success(T value) =>
        new(OperationStatus.Success, value, EmptyErrors);

    public static OperationResult<T> ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(OperationStatus.ValidationFailed, default, errors);

    public static OperationResult<T> ValidationFailed(string field, string message) =>
        ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [field] = [message]
        });

    public static OperationResult<T> NotFound() =>
        new(OperationStatus.NotFound, default, EmptyErrors);

    public static OperationResult<T> Forbidden() =>
        new(OperationStatus.Forbidden, default, EmptyErrors);

    public static OperationResult<T> Conflict() =>
        new(OperationStatus.Conflict, default, EmptyErrors);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
