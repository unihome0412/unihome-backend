namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Verificador de contraseña ficticio, generado una sola vez, para que un correo
/// inexistente consuma un costo de verificación comparable al de una cuenta real.
/// </summary>
public interface IDummyPasswordHashProvider
{
    string Hash { get; }
}
