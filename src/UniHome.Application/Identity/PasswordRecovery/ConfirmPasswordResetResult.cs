namespace UniHome.Application.Identity.PasswordRecovery;

public enum ConfirmPasswordResetStatus
{
    Success,
    InvalidToken,
    ExpiredToken,
    ValidationFailed
}

/// <summary>
/// Resultado de EP-M01-07. Un token inexistente, mal formado o ya usado responde
/// igual (inválido); un token vencido o con intentos agotados exige una nueva
/// solicitud (410).
/// </summary>
public sealed class ConfirmPasswordResetResult
{
    private ConfirmPasswordResetResult(
        ConfirmPasswordResetStatus status,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Status = status;
        ValidationErrors = validationErrors;
    }

    public ConfirmPasswordResetStatus Status { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static ConfirmPasswordResetResult Success() =>
        new(ConfirmPasswordResetStatus.Success, EmptyErrors);

    public static ConfirmPasswordResetResult InvalidToken() =>
        new(ConfirmPasswordResetStatus.InvalidToken, EmptyErrors);

    public static ConfirmPasswordResetResult ExpiredToken() =>
        new(ConfirmPasswordResetStatus.ExpiredToken, EmptyErrors);

    public static ConfirmPasswordResetResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(ConfirmPasswordResetStatus.ValidationFailed, errors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
