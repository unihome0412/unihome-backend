using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Adaptador del PasswordHasher de ASP.NET Core Identity. Produce un hash
/// PBKDF2 con sal aleatoria; nunca cifra de forma reversible.
/// </summary>
public sealed class AspNetCorePasswordHasher : IPasswordHasher
{
    private static readonly object PasswordUser = new();
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return _hasher.HashPassword(PasswordUser, password);
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        return _hasher.VerifyHashedPassword(PasswordUser, passwordHash, password) !=
               Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
    }
}
