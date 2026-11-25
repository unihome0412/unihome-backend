using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Repositorio de integración en memoria que reproduce la restricción de unicidad
/// del índice de MongoDB, sin requerir un clúster real. Se indexa por id (no por
/// correo) para que HU-074 pueda cambiar el correo de una cuenta ya existente.
/// </summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Account> _accounts = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _accounts.Count;
            }
        }
    }

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        lock (_gate)
        {
            if (_accounts.Values.Any(existing => existing.Email.Equals(account.Email)))
            {
                throw new DuplicateEmailException();
            }

            _accounts[account.Id] = account;
        }

        return Task.CompletedTask;
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _accounts.TryGetValue(id, out var account);
            return Task.FromResult(account);
        }
    }

    public Task<Account?> FindByEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        lock (_gate)
        {
            var account = _accounts.Values.SingleOrDefault(candidate => candidate.Email.Equals(email));
            return Task.FromResult(account);
        }
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        lock (_gate)
        {
            if (!_accounts.ContainsKey(account.Id))
            {
                throw new AccountNotFoundException();
            }

            if (_accounts.Values.Any(existing =>
                    existing.Id != account.Id && existing.Email.Equals(account.Email)))
            {
                throw new DuplicateEmailException();
            }

            _accounts[account.Id] = account;
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryChangePasswordAsync(
        Guid accountId,
        string expectedPasswordHash,
        string newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPasswordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);

        lock (_gate)
        {
            if (!_accounts.TryGetValue(accountId, out var account) ||
                account.Status != AccountStatus.Active ||
                !account.IsEmailConfirmed ||
                !string.Equals(account.PasswordHash, expectedPasswordHash, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            account.ChangePassword(newPasswordHash);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Account>> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IEnumerable<Account> candidates = _accounts.Values
                .Where(account => account.Status != AccountStatus.Deleted);

            if (!string.IsNullOrWhiteSpace(query))
            {
                candidates = candidates.Where(account =>
                    account.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    account.Email.Value.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyList<Account> results = candidates
                .OrderBy(account => account.FullName, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(results);
        }
    }
}
