using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.TestSupport;

internal sealed class InMemoryAccountRepository : IAccountRepository
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
}
