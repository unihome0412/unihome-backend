using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica con límites bajos para verificar el 429 sin agotar el límite de las
/// demás pruebas. La configuración se agrega después de la base para prevalecer.
/// </summary>
public sealed class RateLimitedApiFactory : UniHomeApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RateLimit:LoginPermitLimit"] = "2",
                ["Auth:RateLimit:LoginWindowSeconds"] = "60",
                ["Auth:RateLimit:RefreshPermitLimit"] = "2",
                ["Auth:RateLimit:RefreshWindowSeconds"] = "60",
                ["Auth:RateLimit:ConfirmEmailPermitLimit"] = "2",
                ["Auth:RateLimit:ConfirmEmailWindowSeconds"] = "60",
                ["Auth:RateLimit:ResendPermitLimit"] = "2",
                ["Auth:RateLimit:ResendWindowSeconds"] = "60",
                ["Auth:RateLimit:PasswordResetRequestPermitLimit"] = "2",
                ["Auth:RateLimit:PasswordResetRequestWindowSeconds"] = "60",
                ["Auth:RateLimit:PasswordResetConfirmPermitLimit"] = "2",
                ["Auth:RateLimit:PasswordResetConfirmWindowSeconds"] = "60"
            });
        });
    }
}
