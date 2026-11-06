namespace UniHome.Application.Identity.ConfirmEmail;

public enum ConfirmEmailStatus
{
    Confirmed,
    AlreadyConfirmed,
    IncorrectOrUnknown,
    ExpiredCode,
    ValidationFailed
}

/// <summary>
/// Resultado de EP-M01-02. Correo inexistente, código incorrecto, código no
/// emitido y código ya usado se representan igual
/// (<see cref="ConfirmEmailStatus.IncorrectOrUnknown"/>) para no revelar si la
/// cuenta existe ni en qué paso falló.
/// </summary>
public sealed class ConfirmEmailResult
{
    private ConfirmEmailResult(
        ConfirmEmailStatus status,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Status = status;
        ValidationErrors = validationErrors;
    }

    public ConfirmEmailStatus Status { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static ConfirmEmailResult Confirmed() =>
        new(ConfirmEmailStatus.Confirmed, EmptyErrors);

    public static ConfirmEmailResult AlreadyConfirmed() =>
        new(ConfirmEmailStatus.AlreadyConfirmed, EmptyErrors);

    public static ConfirmEmailResult IncorrectOrUnknown() =>
        new(ConfirmEmailStatus.IncorrectOrUnknown, EmptyErrors);

    public static ConfirmEmailResult ExpiredCode() =>
        new(ConfirmEmailStatus.ExpiredCode, EmptyErrors);

    public static ConfirmEmailResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(ConfirmEmailStatus.ValidationFailed, errors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
