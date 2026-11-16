namespace UniHome.Application.Verification.Abstractions;

/// <summary>
/// Construye la URL pública (o relativa) del endpoint que consume un token
/// firmado. La capa API decide el host/base configurado; Application no confía
/// en cabeceras del cliente.
/// </summary>
public interface IDocumentAccessUrlBuilder
{
    string Build(string token);
}
