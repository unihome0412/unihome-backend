using Microsoft.Extensions.Diagnostics.HealthChecks;
using UniHome.Application.Abstractions.RateLimiting;

namespace UniHome.Api.HealthChecks;

/// <summary>
/// Traduce la disponibilidad del almacén distribuido de rate limiting a readiness.
/// En Production Redis es obligatorio; un fallo sostenido debe retirar la réplica
/// del balanceo en lugar de mantener /health/ready en 200 mientras login responde 503.
/// Nunca propaga credenciales, URL ni mensajes de excepción.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IRateLimiterReadinessProbe _readinessProbe;

    public RedisHealthCheck(IRateLimiterReadinessProbe readinessProbe)
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
                .CheckReadinessAsync(cancellationToken)
                .ConfigureAwait(false);

            return readiness.IsReady
                ? HealthCheckResult.Healthy("Control de tasa distribuido disponible.")
                : HealthCheckResult.Unhealthy(
                    readiness.Detail ?? "Control de tasa distribuido no disponible.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // No se expone el mensaje de la excepción: podría contener la URL o el token.
            return HealthCheckResult.Unhealthy(
                "No fue posible verificar el control de tasa distribuido.");
        }
    }
}
