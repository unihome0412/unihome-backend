namespace UniHome.Api.Configuration;

/// <summary>
/// Controla si la API confía en los encabezados de reenvío del proxy de borde
/// (Azure Container Apps termina TLS y reenvía HTTP). Desactivado por defecto:
/// solo debe activarse cuando la topología de red garantiza que esos
/// encabezados los agrega el ingress, no un cliente externo.
/// </summary>
public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    public bool TrustForwardedHeaders { get; set; }

    /// <summary>
    /// Lista explícita de direcciones IP individuales de proxies de confianza (KnownProxies).
    /// </summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// Lista explícita de redes CIDR de proxies de confianza (KnownIPNetworks, ej. "10.0.0.0/16").
    /// </summary>
    public List<string> KnownNetworks { get; set; } = [];
}
