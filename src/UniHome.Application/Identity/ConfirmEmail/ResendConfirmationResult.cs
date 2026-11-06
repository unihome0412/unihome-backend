namespace UniHome.Application.Identity.ConfirmEmail;

public enum ResendConfirmationStatus
{
    /// <summary>
    /// Siempre se devuelve salvo error de formato: exista o no la cuenta, ya esté
    /// confirmada o se haya agotado el límite de reenvíos, la respuesta es igual
    /// (202) para no revelar nada sobre la cuenta (decisión aprobada EP-M01-03).
    /// </summary>
    Accepted,
    ValidationFailed
}

public sealed class ResendConfirmationResult
{
    private ResendConfirmationResult(
        ResendConfirmationStatus status,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Status = status;
        ValidationErrors = validationErrors;
    }

    public ResendConfirmationStatus Status { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static ResendConfirmationResult Accepted() =>
        new(ResendConfirmationStatus.Accepted, EmptyErrors);

    public static ResendConfirmationResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(ResendConfirmationStatus.ValidationFailed, errors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
