namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Genera tokens opacos de recuperación con alta entropía. El valor en claro se
/// entrega una sola vez al remitente; Application solo persiste <see cref="Hash"/>.
/// </summary>
public interface IPasswordResetTokenGenerator
{
    GeneratedPasswordResetToken Generate();

    string Hash(string token);

    /// <summary>
    /// Extrae el identificador interno del token sin validar su secreto. Devuelve
    /// null si el formato no corresponde a un token emitido por este generador.
    /// </summary>
    Guid? TryGetTokenId(string token);
}

public sealed record GeneratedPasswordResetToken(Guid Id, string Value, string Hash);
