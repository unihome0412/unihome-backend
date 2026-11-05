namespace UniHome.Api.Configuration;

/// <summary>
/// Controla la exposición de Swagger UI y del documento OpenAPI nativo fuera
/// de Development. Desactivado por defecto: activarlo es una decisión
/// explícita por entorno (por ejemplo, para una demostración puntual).
/// </summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public bool EnableSwagger { get; set; }
}
