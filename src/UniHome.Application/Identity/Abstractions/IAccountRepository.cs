using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Puerto de persistencia de cuentas. La unicidad del correo normalizado es
/// responsabilidad del adaptador (índice único), no solo de una comprobación previa.
/// </summary>
public interface IAccountRepository
{
    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Account?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el hash de contraseña de una cuenta activa y confirmada solo si el
    /// hash actual coincide con <paramref name="expectedPasswordHash"/>. Es una
    /// actualización atómica de un documento, no una transacción multi-documento.
    /// Devuelve false si la cuenta no existe, no está habilitada o el hash cambió
    /// de forma concurrente.
    /// </summary>
    Task<bool> TryChangePasswordAsync(
        Guid accountId,
        string expectedPasswordHash,
        string newPasswordHash,
        CancellationToken cancellationToken = default);
}
