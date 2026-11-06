namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Hash de contraseña no reversible. La implementación concreta decide el algoritmo
/// y los parámetros; Application solo recibe y verifica el valor protegido.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string password);
}
