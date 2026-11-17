using Microsoft.Extensions.Options;
using UniHome.Application.Verification.Abstractions;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Api.Verification;

/// <summary>
/// Construye enlaces relativos salvo que exista una base pública HTTPS
/// configurada. Nunca usa la cabecera Host del cliente para evitar enlaces
/// envenenados.
/// </summary>
public sealed class DocumentAccessUrlBuilder : IDocumentAccessUrlBuilder
{
    private const string PathPrefix = "/api/v1/expedientes/documentos/";

    private readonly DocumentSecurityOptions _options;

    public DocumentAccessUrlBuilder(IOptions<DocumentSecurityOptions> options)
    {
        _options = options.Value;
    }

    public string Build(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var path = PathPrefix + Uri.EscapeDataString(token);

        return string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? path
            : _options.PublicBaseUrl.TrimEnd('/') + path;
    }
}
