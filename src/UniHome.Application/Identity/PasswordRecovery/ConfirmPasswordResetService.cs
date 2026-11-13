using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.PasswordRecovery;

/// <summary>
/// EP-M01-07 (HU-006). Valida un token de un solo uso, cambia la contraseña y
/// revoca todas las sesiones de refresh de la cuenta. El consumo del token es
/// atómico: dos confirmaciones concurrentes no pueden cambiar la contraseña dos veces.
/// </summary>
public sealed class ConfirmPasswordResetService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;
    private const int MaxTokenLength = 512;
    private const string RevocationReason = "password_reset";

    private readonly IAccountRepository _accounts;
    private readonly IPasswordResetRepository _resets;
    private readonly IPasswordResetTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshSessionRepository _sessions;
    private readonly ISystemClock _clock;

    public ConfirmPasswordResetService(
        IAccountRepository accounts,
        IPasswordResetRepository resets,
        IPasswordResetTokenGenerator tokenGenerator,
        IPasswordHasher passwordHasher,
        IRefreshSessionRepository sessions,
        ISystemClock clock)
    {
        _accounts = accounts;
        _resets = resets;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<ConfirmPasswordResetResult> ConfirmAsync(
        ConfirmPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = Validate(command);

        if (errors.Count > 0)
        {
            return ConfirmPasswordResetResult.ValidationFailed(errors);
        }

        var resetId = _tokenGenerator.TryGetTokenId(command.Token!);

        if (resetId is null)
        {
            return ConfirmPasswordResetResult.InvalidToken();
        }

        var reset = await _resets
            .FindByIdAsync(resetId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (reset is null)
        {
            return ConfirmPasswordResetResult.InvalidToken();
        }

        var now = _clock.UtcNow;
        var tokenHash = _tokenGenerator.Hash(command.Token!);
        var validation = reset.Validate(tokenHash, now);

        if (validation == PasswordResetValidation.AlreadyUsed)
        {
            return ConfirmPasswordResetResult.InvalidToken();
        }

        if (validation == PasswordResetValidation.Expired)
        {
            return ConfirmPasswordResetResult.ExpiredToken();
        }

        if (validation == PasswordResetValidation.Invalid)
        {
            return await RegisterFailedAttemptAsync(resetId.Value, now, cancellationToken)
                .ConfigureAwait(false);
        }

        var account = await _accounts
            .FindByIdAsync(reset.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null ||
            account.Status != AccountStatus.Active ||
            !account.IsEmailConfirmed)
        {
            return ConfirmPasswordResetResult.InvalidToken();
        }

        // Solo se calcula el hash caro después de validar el token y la cuenta.
        var passwordHash = _passwordHasher.HashPassword(command.NewPassword!);

        var consumed = await _resets
            .TryConsumeAsync(resetId.Value, tokenHash, now, cancellationToken)
            .ConfigureAwait(false);

        if (!consumed)
        {
            return await ClassifyFailedConsumptionAsync(
                    resetId.Value,
                    tokenHash,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        // Invalida cualquier otro enlace pendiente antes de cambiar la contraseña.
        await _resets
            .InvalidateAllForAccountAsync(account.Id, now, cancellationToken)
            .ConfigureAwait(false);

        // Revocar primero las sesiones evita dejar sesiones vivas si la escritura
        // final de la cuenta falla. El usuario puede volver a solicitar el enlace.
        await _sessions
            .RevokeAllForAccountAsync(
                account.Id,
                now,
                RevocationReason,
                cancellationToken)
            .ConfigureAwait(false);

        // CAS de un solo documento: si dos tokens de la misma cuenta llegaran a
        // confirmarse a la vez, solo uno puede cambiar el hash esperado.
        var passwordChanged = await _accounts
            .TryChangePasswordAsync(
                account.Id,
                account.PasswordHash,
                passwordHash,
                cancellationToken)
            .ConfigureAwait(false);

        return passwordChanged
            ? ConfirmPasswordResetResult.Success()
            : ConfirmPasswordResetResult.InvalidToken();
    }

    private async Task<ConfirmPasswordResetResult> RegisterFailedAttemptAsync(
        Guid resetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updated = await _resets
            .RegisterFailedAttemptAsync(resetId, now, cancellationToken)
            .ConfigureAwait(false);

        if (updated is null)
        {
            var latest = await _resets
                .FindByIdAsync(resetId, cancellationToken)
                .ConfigureAwait(false);

            if (latest is null || latest.IsConsumed)
            {
                return ConfirmPasswordResetResult.InvalidToken();
            }

            return latest.IsExpired(now) || latest.HasExceededAttempts
                ? ConfirmPasswordResetResult.ExpiredToken()
                : ConfirmPasswordResetResult.InvalidToken();
        }

        return updated.HasExceededAttempts
            ? ConfirmPasswordResetResult.ExpiredToken()
            : ConfirmPasswordResetResult.InvalidToken();
    }

    private async Task<ConfirmPasswordResetResult> ClassifyFailedConsumptionAsync(
        Guid resetId,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var latest = await _resets
            .FindByIdAsync(resetId, cancellationToken)
            .ConfigureAwait(false);

        if (latest is null || latest.IsConsumed)
        {
            return ConfirmPasswordResetResult.InvalidToken();
        }

        if (latest.IsExpired(now) || latest.HasExceededAttempts)
        {
            return ConfirmPasswordResetResult.ExpiredToken();
        }

        return latest.Validate(tokenHash, now) == PasswordResetValidation.Expired
            ? ConfirmPasswordResetResult.ExpiredToken()
            : ConfirmPasswordResetResult.InvalidToken();
    }

    private static Dictionary<string, string[]> Validate(
        ConfirmPasswordResetCommand command)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            Add(errors, "token", "El token de recuperación es obligatorio.");
        }
        else if (command.Token.Length > MaxTokenLength)
        {
            Add(errors, "token", "El token de recuperación no tiene un formato válido.");
        }

        if (string.IsNullOrEmpty(command.NewPassword))
        {
            Add(errors, "nuevaContrasena", "La nueva contraseña es obligatoria.");
        }
        else if (command.NewPassword.Length < MinPasswordLength)
        {
            Add(
                errors,
                "nuevaContrasena",
                $"La nueva contraseña debe tener al menos {MinPasswordLength} caracteres.");
        }
        else if (command.NewPassword.Length > MaxPasswordLength)
        {
            Add(
                errors,
                "nuevaContrasena",
                $"La nueva contraseña no debe superar {MaxPasswordLength} caracteres.");
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static void Add(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }
}
