using Microsoft.Extensions.Diagnostics.HealthChecks;
using UniHome.Application.Abstractions.Storage;

namespace UniHome.Api.HealthChecks;

/// <summary>
/// Traduce el puerto de storage a un health check de readiness.
/// La ausencia de configuración es Unhealthy: nunca se reporta un falso OK.
/// </summary>
public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly IStorageReadinessProbe _readinessProbe;

    public StorageHealthCheck(IStorageReadinessProbe readinessProbe)
    {
        _readinessProbe = readinessProbe;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var readiness = await _readinessProbe
                .CheckAsync(cancellationToken)
                .ConfigureAwait(false);

            return readiness.Status switch
            {
                StorageReadinessStatus.Ready =>
                    HealthCheckResult.Healthy("Almacenamiento disponible."),
                StorageReadinessStatus.NotConfigured =>
                    HealthCheckResult.Unhealthy(
                        "Almacenamiento no configurado: defina MongoDb:ConnectionString y MongoDb:DatabaseName."),
                _ => HealthCheckResult.Unhealthy(
                    readiness.Detail ?? "Almacenamiento no disponible.")
            };
        }
        catch (Exception)
        {
            // No se propaga el mensaje de la excepción: podría contener datos de conexión.
            return HealthCheckResult.Unhealthy("No fue posible verificar el almacenamiento.");
        }
    }
}
