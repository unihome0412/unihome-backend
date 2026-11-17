namespace UniHome.Api.Configuration;

/// <summary>
/// Controla la exposición de Swagger UI y del documento OpenAPI nativo.
/// Desactivado por defecto: activarlo es una decisión explícita por entorno y
/// nunca habilita acceso anónimo; la policy administrativa protege la interfaz,
/// el JSON y sus assets.
/// </summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public bool EnableSwagger { get; set; }
}
