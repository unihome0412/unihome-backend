using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class AspNetCorePasswordHasherTests
{
    private const string Password = "ClaveSegura1";

    private readonly AspNetCorePasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_NoDevuelveTextoPlanoYUsaSalAleatoria()
    {
        var first = _hasher.HashPassword(Password);
        var second = _hasher.HashPassword(Password);

        Assert.NotEqual(Password, first);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(Password, first, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyPassword_AceptaLaContrasenaCorrecta()
    {
        var hash = _hasher.HashPassword(Password);

        Assert.True(_hasher.VerifyPassword(hash, Password));
    }

    [Fact]
    public void VerifyPassword_RechazaLaContrasenaIncorrecta()
    {
        var hash = _hasher.HashPassword(Password);

        Assert.False(_hasher.VerifyPassword(hash, "OtraClave1"));
    }
}
