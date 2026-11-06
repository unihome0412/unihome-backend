namespace UniHome.Application.Identity.RefreshSessions;

public enum RefreshSessionStatus
{
    Success,
    Invalid,
    ValidationFailed
}

/// <summary>
/// Resultado de renovación. Todos los fallos de token, sesión o estado de cuenta
/// se agrupan en <see cref="Invalid"/> para no filtrar información.
/// </summary>
public sealed class RefreshSessionResult
{
    private RefreshSessionResult(
        RefreshSessionStatus status,
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt)
    {
        Status = status;
        ValidationErrors = validationErrors;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
    }

    public RefreshSessionStatus Status { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public string? AccessToken { get; }

    public string? RefreshToken { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public bool Succeeded => Status == RefreshSessionStatus.Success;

    public static RefreshSessionResult Invalid() =>
        new(RefreshSessionStatus.Invalid, EmptyErrors, null, null, null);

    public static RefreshSessionResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(RefreshSessionStatus.ValidationFailed, errors, null, null, null);

    public static RefreshSessionResult Success(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt) =>
        new(RefreshSessionStatus.Success, EmptyErrors, accessToken, refreshToken, expiresAt);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
