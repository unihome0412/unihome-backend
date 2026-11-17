namespace UniHome.Application.Identity.Login;

/// <summary>
/// Resultado de validar credenciales sin emitir JWT ni crear una sesión de
/// refresh. Se usa para el login documental del navegador, que reutiliza las
/// mismas reglas de cuenta, contraseña y estado que el login móvil.
/// </summary>
public sealed record LoginAuthenticationResult(
    LoginStatus Status,
    Guid? AccountId,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public bool Succeeded => Status == LoginStatus.Success;

    public static LoginAuthenticationResult Success(Guid accountId) =>
        new(LoginStatus.Success, accountId, EmptyErrors);

    public static LoginAuthenticationResult InvalidCredentials() =>
        new(LoginStatus.InvalidCredentials, null, EmptyErrors);

    public static LoginAuthenticationResult EmailNotConfirmed() =>
        new(LoginStatus.EmailNotConfirmed, null, EmptyErrors);

    public static LoginAuthenticationResult Suspended() =>
        new(LoginStatus.Suspended, null, EmptyErrors);

    public static LoginAuthenticationResult Deleted() =>
        new(LoginStatus.Deleted, null, EmptyErrors);

    public static LoginAuthenticationResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(LoginStatus.ValidationFailed, null, errors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
}
