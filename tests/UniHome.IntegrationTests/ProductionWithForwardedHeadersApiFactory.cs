namespace UniHome.IntegrationTests;

/// <summary>
/// Misma fábrica de <see cref="ProductionApiFactory"/> pero con
/// <c>Proxy:TrustForwardedHeaders=true</c>, para probar el comportamiento
/// cuando la configuración lo activa explícitamente.
/// </summary>
public sealed class ProductionWithForwardedHeadersApiFactory : ProductionApiFactory
{
    protected override bool TrustForwardedHeaders => true;
}
