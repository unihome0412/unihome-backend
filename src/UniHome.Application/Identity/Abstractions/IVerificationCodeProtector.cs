namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Protección no reversible del código de confirmación (hash/HMAC).
/// El código en claro solo se entrega al remitente abstracto; nunca se persiste.
/// </summary>
public interface IVerificationCodeProtector
{
    string Protect(string code);

    bool Matches(string code, string protectedCode);
}
