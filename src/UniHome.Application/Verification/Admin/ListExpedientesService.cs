using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Admin;

/// <summary>
/// EP-M06-04 (HU-023): bandeja administrativa de primeras publicaciones. Los
/// sospechosos se ordenan primero y el contador incluye Pendiente + En revisión.
/// </summary>
public sealed class ListExpedientesService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 100_000;

    private readonly IVerificationRepository _expedientes;
    private readonly IAccountRepository _accounts;

    public ListExpedientesService(
        IVerificationRepository expedientes,
        IAccountRepository accounts)
    {
        _expedientes = expedientes;
        _accounts = accounts;
    }

    public async Task<OperationResult<ExpedientePageView>> ListAsync(
        EstadoExpediente? state,
        bool suspiciousFirst,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > MaxPage)
        {
            return OperationResult<ExpedientePageView>.ValidationFailed(
                "page",
                $"La página debe estar entre 1 y {MaxPage}.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return OperationResult<ExpedientePageView>.ValidationFailed(
                "pageSize",
                $"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
        }

        var skip = checked((page - 1) * pageSize);
        var expedientes = await _expedientes
            .ListAsync(state, suspiciousFirst, skip, pageSize, cancellationToken)
            .ConfigureAwait(false);
        var total = await _expedientes
            .CountAsync(state, cancellationToken)
            .ConfigureAwait(false);
        var pending = await _expedientes
            .CountPendingReviewAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<ExpedienteResumenView>(expedientes.Count);

        foreach (var expediente in expedientes)
        {
            var account = await _accounts
                .FindByIdAsync(expediente.OwnerAccountId, cancellationToken)
                .ConfigureAwait(false);

            items.Add(new ExpedienteResumenView(
                expediente.Id,
                expediente.Folio,
                account?.FullName ?? "Solicitante no disponible",
                expediente.CreatedAt,
                expediente.State,
                expediente.IsSuspicious));
        }

        return OperationResult<ExpedientePageView>.Success(
            new ExpedientePageView(items, total, pending));
    }
}
