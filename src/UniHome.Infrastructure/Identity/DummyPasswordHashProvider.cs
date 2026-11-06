using System.Security.Cryptography;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Genera una sola vez un hash de contraseña ficticio con el mismo algoritmo de
/// Identity. Su único fin es igualar el costo de un correo inexistente.
/// </summary>
internal sealed class DummyPasswordHashProvider : IDummyPasswordHashProvider
{
    public DummyPasswordHashProvider(IPasswordHasher passwordHasher)
    {
        Hash = passwordHasher.HashPassword(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public string Hash { get; }
}
