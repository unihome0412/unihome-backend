using UniHome.Application.Common;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Abstractions;

public interface IVerificationRepository
{
    Task AddAsync(Expediente expediente, CancellationToken cancellationToken = default);

    Task<Expediente?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Expediente?> FindByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Titulares con expediente Aprobado, para derivar la insignia de arrendador
    /// verificado sin consultar uno por uno.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListApprovedOwnerIdsAsync(
        IReadOnlyCollection<Guid> ownerAccountIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplazo con concurrencia optimista sobre <c>updatedAt</c>. Devuelve false
    /// si otra solicitud modificó el expediente desde la lectura.
    /// </summary>
    Task<bool> TryReplaceAsync(
        Expediente expediente,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Expediente>> ListAsync(
        EstadoExpediente? state,
        bool suspiciousFirst,
        int skip,
        int limit,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        EstadoExpediente? state,
        CancellationToken cancellationToken = default);

    /// <summary>Contador de primeras publicaciones por revisar (Pendiente + En revisión).</summary>
    Task<long> CountPendingReviewAsync(CancellationToken cancellationToken = default);
}

public sealed class ExpedienteAlreadyExistsException : Exception
{
    public ExpedienteAlreadyExistsException()
        : base("El expediente ya existe.")
    {
    }
}
