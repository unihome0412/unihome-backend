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
}
