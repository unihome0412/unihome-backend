using Microsoft.OpenApi;
using UniHome.Application.Units.Photos;
using UniHome.Domain.Units;

namespace UniHome.Api.Configuration;

/// <summary>
/// Corrige el contrato de POST /api/v1/unidades/{unitId}/fotos. El runtime
/// enlaza [FromForm] con IFormFile repetible y exige multipart/form-data; el
/// generador nativo describe application/x-www-form-urlencoded. El esquema se
/// expresa como array de binarios para que Android y RESTler no envíen texto.
/// </summary>
internal static class UnitPhotosOpenApiContract
{
    public const string RelativePath = "api/v1/unidades/{unitId}/fotos";
    public const string Method = "POST";
    public const string FormField = "Fotos";

    private const string Description =
        "Carga hasta 10 fotografías JPEG o PNG en una sola solicitud multipart. " +
        "Cada archivo puede pesar como máximo 5 MB y la solicitud completa 52 MB. " +
        "El contenido se valida por firma binaria, no por extensión.";

    public static void Apply(OpenApiOperation operation)
    {
        var photoSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "binary",
            Description =
                $"Archivo JPEG o PNG de hasta " +
                $"{UnitPhotoValidator.MaxBytesPerPhoto / (1024 * 1024)} MB."
        };

        var photosSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Description =
                $"Hasta {Unit.MaxPhotos} fotografías; el campo se repite una vez por archivo.",
            Items = photoSchema,
            MinItems = 1,
            MaxItems = Unit.MaxPhotos
        };

        var formSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                [FormField] = photosSchema
            },
            Required = new HashSet<string>(StringComparer.Ordinal) { FormField }
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = Description,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["multipart/form-data"] = new()
                {
                    Schema = formSchema,
                    Encoding = new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal)
                    {
                        [FormField] = new()
                        {
                            Style = ParameterStyle.Form,
                            Explode = true
                        }
                    }
                }
            }
        };
    }
}
