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

    /// <summary>
    /// Indica si la cuenta aceptó alguna versión de términos. Sprint 4 no fija
    /// una versión vigente; el envío a revisión exige al menos una aceptación.
    /// </summary>
    Task<bool> ExistsAnyAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}
