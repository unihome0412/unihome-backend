namespace UniHome.Application.Identity.PasswordRecovery;

public enum RequestPasswordResetStatus
{
    Accepted,
    ValidationFailed
}

/// <summary>
/// Resultado de EP-M01-06. Un correo inexistente, suspendido, eliminado o no
/// confirmado produce el mismo <see cref="Accepted"/> que una cuenta válida.
/// </summary>
public sealed class RequestPasswordResetResult
{
    private RequestPasswordResetResult(
        RequestPasswordResetStatus status,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Status = status;
        ValidationErrors = validationErrors;
    }

    public RequestPasswordResetStatus Status { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static RequestPasswordResetResult Accepted() =>
        new(RequestPasswordResetStatus.Accepted, EmptyErrors);

    public static RequestPasswordResetResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(RequestPasswordResetStatus.ValidationFailed, errors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
