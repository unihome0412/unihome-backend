using UniHome.Domain.Publications;

namespace UniHome.Application.Publications.Abstractions;

public interface ITermsAcceptanceRepository
{
    Task AddAsync(
        TermsAcceptance acceptance,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid accountId,
        string version,
        CancellationToken cancellationToken = default);
}
