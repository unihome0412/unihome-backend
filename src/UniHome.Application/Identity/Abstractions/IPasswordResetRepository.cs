using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Persistencia de solicitudes de recuperación. El adaptador debe garantizar el
/// consumo atómico del token para que solo una confirmación concurrente gane.
/// </summary>
public interface IPasswordResetRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consume el token solo si sigue vigente, no fue usado, conserva el mismo
    /// hash y no agotó sus intentos. Devuelve false en cualquier otro caso.
    /// </summary>
    Task<bool> TryConsumeAsync(
        Guid id,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa el contador de intentos de un token vigente. Devuelve el estado
    /// actualizado o null si el token ya venció, fue consumido o agotó el límite.
    /// </summary>
    Task<PasswordResetToken?> RegisterFailedAttemptAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida todas las solicitudes pendientes de la cuenta. Se usa al emitir
    /// un token nuevo y después de un restablecimiento exitoso.
    /// </summary>
    Task InvalidateAllForAccountAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
