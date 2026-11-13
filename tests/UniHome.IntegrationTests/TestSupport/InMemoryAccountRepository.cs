using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Repositorio de integración en memoria que reproduce la restricción de unicidad
/// del índice de MongoDB, sin requerir un clúster real.
/// </summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Account> _accounts = new(StringComparer.Ordinal);

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
            if (!_accounts.TryAdd(account.Email.Value, account))
            {
                throw new DuplicateEmailException();
            }
        }

        return Task.CompletedTask;
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_accounts.Values.SingleOrDefault(account => account.Id == id));
        }
    }

    public Task<Account?> FindByEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        lock (_gate)
        {
            _accounts.TryGetValue(email.Value, out var account);
            return Task.FromResult(account);
        }
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        lock (_gate)
        {
            if (!_accounts.ContainsKey(account.Email.Value))
            {
                throw new AccountNotFoundException();
            }

            _accounts[account.Email.Value] = account;
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
            var account = _accounts.Values.SingleOrDefault(candidate => candidate.Id == accountId);

            if (account is null ||
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
}
