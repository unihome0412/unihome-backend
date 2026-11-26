using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UniHome.IntegrationTests;

/// <summary>
/// Misma fábrica que <see cref="UniHomeApiFactory"/> pero con un TTL de
/// output cache corto y determinista, para poder probar el comportamiento
/// real de caché (servir una respuesta obsoleta dentro de la ventana, y
/// fresca después de que expira) sin depender del valor de producción
/// (20 s) ni de esperas largas en la suite de pruebas.
/// </summary>
public sealed class ShortOutputCacheApiFactory : UniHomeApiFactory
{
    public const int PublicTtlSeconds = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OutputCache:PublicTtlSeconds"] = PublicTtlSeconds.ToString()
            });
        });
    }
}
