using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Abstractions;

public enum DocumentUrlVariant
{
    Full,
    Thumbnail
}

public sealed record DocumentAccessToken(
    Guid ExpedienteId,
    TipoDocumentoVerificacion DocumentType,
    int Version,
    DocumentUrlVariant Variant,
    DateTimeOffset ExpiresAt);

public sealed record DocumentAccessGrant(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Firma y valida enlaces temporales. El token no contiene el binario ni datos
/// personales y solo puede consumirse con sesión administrativa válida.
/// </summary>
public interface IDocumentUrlSigner
{
    DocumentAccessGrant Create(
        Guid expedienteId,
        TipoDocumentoVerificacion documentType,
        int version,
        DocumentUrlVariant variant,
        DateTimeOffset now);

    DocumentAccessToken? Validate(string token, DateTimeOffset now);
}
