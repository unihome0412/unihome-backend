using UniHome.Application.Abstractions.Caching;

namespace UniHome.Api.Caching;

/// <summary>
/// Nombres de política y tags de Output Cache (Bloque 2 de la auditoría).
/// Solo cubre los cuatro GET públicos pedidos: búsqueda de universidades,
/// mapa, lista y tarjeta de exploración. Nunca se aplica a rutas
/// autenticadas ni con datos personales — ver <c>Program.cs</c> para las
/// reglas exactas de cada política.
/// </summary>
public static class OutputCachePolicies
{
    public const string PublicMap = "exploracion-mapa-publica";
    public const string PublicListAndCard = "exploracion-lista-tarjeta-publica";
    public const string PublicUniversitySearch = "universidades-busqueda-publica";

    /// <summary>Tag invalidado al aprobar/rechazar un expediente (la unidad
    /// cambia de estado de publicación). Ver <see cref="PublicCacheTags"/>.</summary>
    public const string ExplorationTag = PublicCacheTags.Exploration;

    /// <summary>Tag invalidado al crear, editar o cambiar el estado de una
    /// universidad. Ver <see cref="PublicCacheTags"/>.</summary>
    public const string UniversitiesTag = PublicCacheTags.Universities;
}
