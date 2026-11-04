using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Sessions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Login;

/// <summary>
/// HU-004: inicio de sesión único con correo y contraseña. No solicita rol,
/// exige correo confirmado y devuelve mensajes genéricos ante credenciales
/// inválidas. La cuenta eliminada se trata como credencial inválida.
/// </summary>
public sealed class LoginService
{
    private const int MaxPasswordLength = 128;

    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDummyPasswordHashProvider _dummyPasswordHash;
    private readonly ISystemClock _clock;
    private readonly IRefreshSessionRepository _sessions;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IAuthTokenSettings _tokenSettings;

    public LoginService(
        IAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IDummyPasswordHashProvider dummyPasswordHash,
        ISystemClock clock,
        IRefreshSessionRepository sessions,
        IRefreshTokenGenerator refreshTokens,
        IAccessTokenIssuer accessTokens,
        IAuthTokenSettings tokenSettings)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _dummyPasswordHash = dummyPasswordHash;
        _clock = clock;
        _sessions = sessions;
        _refreshTokens = refreshTokens;
        _accessTokens = accessTokens;
        _tokenSettings = tokenSettings;
    }

    public async Task<LoginResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _clock.UtcNow;
        var (email, errors) = Validate(command);

        if (email is null || errors.Count > 0)
        {
            return LoginResult.ValidationFailed(errors);
        }

        var account = await _accounts
            .FindByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            // Mismo costo de verificación que una cuenta existente. No se registra
            // el correo ni el resultado.
            _passwordHasher.VerifyPassword(_dummyPasswordHash.Hash, command.Password!);
            return LoginResult.InvalidCredentials();
        }

        if (!_passwordHasher.VerifyPassword(account.PasswordHash, command.Password!))
        {
            return LoginResult.InvalidCredentials();
        }

        if (account.Status == AccountStatus.Deleted)
        {
            return LoginResult.Deleted();
        }

        if (account.Status == AccountStatus.Suspended)
        {
            return LoginResult.Suspended();
        }

        if (!account.IsEmailConfirmed)
        {
            return LoginResult.EmailNotConfirmed();
        }

        var sessionId = Guid.NewGuid();
        var refreshToken = _refreshTokens.Generate(sessionId);
        var refreshExpiresAt = now.Add(_tokenSettings.RefreshTokenLifetime);

        var session = new RefreshSession(
            sessionId,
            account.Id,
            refreshToken.Hash,
            now,
            refreshExpiresAt,
            [],
            RevokedAt: null,
            RevokedReason: null,
            now,
            now);

        await _sessions
            .CreateAsync(session, cancellationToken)
            .ConfigureAwait(false);

        var accessToken = _accessTokens.Issue(account.Id, sessionId, now);

        return LoginResult.Success(
            accessToken.Value,
            refreshToken.Value,
            accessToken.ExpiresAt);
    }

    private static (EmailAddress? Email, IReadOnlyDictionary<string, string[]> Errors) Validate(
        LoginCommand command)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.Add(message);
        }

        EmailAddress? email = null;

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            Add("correo", "El correo electrónico es obligatorio.");
        }
        else
        {
            try
            {
                email = EmailAddress.Create(command.Email);
            }
            catch (ArgumentException)
            {
                Add("correo", "El correo electrónico no tiene un formato válido.");
            }
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            Add("contrasena", "La contraseña es obligatoria.");
        }
        else if (command.Password.Length > MaxPasswordLength)
        {
            // Se acota antes de invocar PBKDF2 para evitar consumo desmedido.
            Add("contrasena", $"La contraseña no debe superar {MaxPasswordLength} caracteres.");
        }

        var convertedErrors = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);

        return (email, convertedErrors);
    }
}
