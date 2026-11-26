namespace UniHome.Application.Abstractions.Caching;

/// <summary>
/// Tags del output cache de GET públicos (Bloque 2 de la auditoría),
/// compartidos entre la capa Api (que los declara en sus políticas de
/// caché) y Application (que invalida por tag cuando cambia el dato
/// subyacente). Vive en Application, no en Api, porque los servicios que
/// disparan la invalidación (aprobar/rechazar un expediente, crear/editar
/// una universidad) están en esta capa y no pueden depender de Api.
/// </summary>
public static class PublicCacheTags
{
    /// <summary>Mapa, lista y tarjeta de exploración: se invalida cuando una
    /// unidad cambia de estado de publicación (aprobar/rechazar un
    /// expediente).</summary>
    public const string Exploration = "exploracion-publica";

    /// <summary>Búsqueda pública de universidades: se invalida al crear,
    /// editar o cambiar el estado de una universidad.</summary>
    public const string Universities = "universidades-publico";
}
