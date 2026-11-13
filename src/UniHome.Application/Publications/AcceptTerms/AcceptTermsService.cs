using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Publications.Exceptions;
using UniHome.Domain.Publications;

namespace UniHome.Application.Publications.AcceptTerms;

/// <summary>
/// EP-M05-07 (HU-091): registra la versión aceptada de términos. No envía a
/// revisión ni crea el expediente de M06 (Sprint 4).
/// </summary>
public sealed class AcceptTermsService
{
    private readonly ITermsAcceptanceRepository _acceptances;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public AcceptTermsService(
        ITermsAcceptanceRepository acceptances,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _acceptances = acceptances;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<TermsAcceptance>> AcceptAsync(
        Guid accountId,
        AcceptTermsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _eligibility.IsEligibleAsync(accountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<TermsAcceptance>.Forbidden();
        }

        TermsAcceptance acceptance;

        try
        {
            acceptance = TermsAcceptance.Create(
                accountId,
                command.VersionTerminos,
                _clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<TermsAcceptance>.ValidationFailed(
                "versionTerminos",
                exception.Message);
        }

        if (await _acceptances
                .ExistsAsync(accountId, acceptance.Version, cancellationToken)
                .ConfigureAwait(false))
        {
            return OperationResult<TermsAcceptance>.Conflict();
        }

        try
        {
            await _acceptances.AddAsync(acceptance, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateTermsAcceptanceException)
        {
            return OperationResult<TermsAcceptance>.Conflict();
        }

        return OperationResult<TermsAcceptance>.Success(acceptance);
    }
}
