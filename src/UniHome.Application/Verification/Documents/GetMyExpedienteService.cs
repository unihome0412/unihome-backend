using UniHome.Application.Common;
using UniHome.Application.Verification.Abstractions;

namespace UniHome.Application.Verification.Documents;

/// <summary>
/// EP-M06-03 (HU-026): estado de verificación del propio solicitante. Expone
/// únicamente recepción, progreso automático, decisión y motivo; nunca señales de
/// riesgo, OCR, ELA ni datos de otros usuarios.
/// </summary>
public sealed class GetMyExpedienteService
{
    private readonly IVerificationRepository _expedientes;

    public GetMyExpedienteService(IVerificationRepository expedientes)
    {
        _expedientes = expedientes;
    }

    public async Task<OperationResult<MiExpedienteView>> GetAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        var expediente = await _expedientes
            .FindByOwnerAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        return expediente is null
            ? OperationResult<MiExpedienteView>.NotFound()
            : OperationResult<MiExpedienteView>.Success(
                MiExpedienteView.FromDomain(expediente));
    }
}
