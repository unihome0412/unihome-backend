using Microsoft.OpenApi;
using UniHome.Application.Verification.Documents;

namespace UniHome.Api.Configuration;

/// <summary>
/// Documenta las cargas M06 como multipart con un archivo binario real. El
/// generador nativo tiende a describir formularios urlencoded para IFormFile.
/// </summary>
internal static class DocumentUploadOpenApiContract
{
    public const string InePath = "api/v1/expedientes/mi-expediente/ine";
    public const string ProofPath = "api/v1/expedientes/mi-expediente/comprobante";
    public const string Method = "POST";
    public const string FileField = "Archivo";
    public const string NameField = "NombreArchivoOriginal";

    public static bool AppliesTo(string? relativePath) =>
        string.Equals(relativePath, InePath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relativePath, ProofPath, StringComparison.OrdinalIgnoreCase);

    public static void Apply(OpenApiOperation operation)
    {
        var fileSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "binary",
            Description =
                "JPG, PNG o PDF de hasta 10 MB. Se valida la firma binaria real y " +
                "se rechaza contenido activo PDF."
        };

        var formSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                [FileField] = fileSchema,
                [NameField] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MaxLength = 255,
                    Description = "Nombre original opcional para mostrar en el expediente."
                }
            },
            Required = new HashSet<string>(StringComparer.Ordinal) { FileField }
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description =
                "Documento privado. Se cifra en reposo y solo se entrega por enlace " +
                "temporal a un administrador autenticado.",
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["multipart/form-data"] = new()
                {
                    Schema = formSchema,
                    Encoding = new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal)
                    {
                        [FileField] = new() { Style = ParameterStyle.Form, Explode = true }
                    }
                }
            }
        };
    }
}
