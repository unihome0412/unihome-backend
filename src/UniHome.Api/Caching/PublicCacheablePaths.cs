using Microsoft.AspNetCore.Http;

namespace UniHome.Api.Caching;

/// <summary>
/// Coincidencia explícita de las únicas cuatro rutas GET públicas elegibles
/// para compresión y output cache (Bloque 2 de la auditoría). Se usa para
/// ramificar el pipeline con <c>UseWhen</c> en <c>Program.cs</c>: fuera de
/// estas rutas, ni la compresión ni el output cache middleware se ejecutan.
/// Lista cerrada a propósito, no un patrón genérico — agregar una ruta nueva
/// aquí es una decisión explícita, no un efecto secundario de una regex
/// amplia.
/// </summary>
internal static class PublicCacheablePaths
{
    private const string Universidades = "/api/v1/universidades";
    private const string ExploracionMapa = "/api/v1/exploracion/mapa";
    private const string ExploracionLista = "/api/v1/exploracion/lista";
    private const string UnidadesPrefix = "/api/v1/unidades/";
    private const string TarjetaSuffix = "/tarjeta";

    public static bool Matches(PathString path)
    {
        if (path.Equals(Universidades, StringComparison.OrdinalIgnoreCase) ||
            path.Equals(ExploracionMapa, StringComparison.OrdinalIgnoreCase) ||
            path.Equals(ExploracionLista, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsUnitCardPath(path);
    }

    private static bool IsUnitCardPath(PathString path)
    {
        var value = path.Value;

        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith(UnidadesPrefix, StringComparison.OrdinalIgnoreCase) ||
            !value.EndsWith(TarjetaSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var unitId = value[UnidadesPrefix.Length..^TarjetaSuffix.Length];

        // Un solo segmento sin más "/": evita que algo como
        // "/api/v1/unidades/x/y/tarjeta" coincida por accidente.
        return unitId.Length > 0 && !unitId.Contains('/');
    }
}
