using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FixedClock : ISystemClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}

internal sealed class RecordingPasswordHasher : IPasswordHasher
{
    public int HashCalls { get; private set; }

    public List<(string PasswordHash, string Password)> VerifyCalls { get; } = [];

    public string HashPassword(string password)
    {
        HashCalls++;
        return $"hash:{password}";
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        VerifyCalls.Add((passwordHash, password));
        return string.Equals(passwordHash, $"hash:{password}", StringComparison.Ordinal);
    }
}

internal sealed class FixedVerificationCodeGenerator : IVerificationCodeGenerator
{
    private readonly string _code;

    public FixedVerificationCodeGenerator(string code)
    {
        _code = code;
    }

    public string Generate() => _code;
}

internal sealed class PrefixVerificationCodeProtector : IVerificationCodeProtector
{
    public string Protect(string code) => $"protected:{code}";

    public bool Matches(string code, string protectedCode) =>
        string.Equals(Protect(code), protectedCode, StringComparison.Ordinal);
}

/// <summary>
/// Protector de prueba cuyo resultado es Base64 válido, igual que el real
/// (<c>HmacVerificationCodeProtector</c>). <see cref="VerificationCode.Matches"/>
/// decodifica <c>CodeHash</c> como Base64; <see cref="PrefixVerificationCodeProtector"/>
/// no sirve para pruebas que validan/confirman un código real.
/// </summary>
internal sealed class Base64VerificationCodeProtector : IVerificationCodeProtector
{
    public string Protect(string code) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected:{code}"));

    public bool Matches(string code, string protectedCode) =>
        string.Equals(Protect(code), protectedCode, StringComparison.Ordinal);
}

internal sealed class RecordingVerificationEmailSender : IVerificationEmailSender
{
    private readonly List<(string Email, string Code)> _messages = [];

    public IReadOnlyList<(string Email, string Code)> Messages => _messages;

    public Task<bool> SendAsync(
        EmailAddress email,
        string code,
        CancellationToken cancellationToken = default)
    {
        _messages.Add((email.Value, code));
        return Task.FromResult(true);
    }
}

internal sealed class ThrowingVerificationEmailSender : IVerificationEmailSender
{
    public Task<bool> SendAsync(
        EmailAddress email,
        string code,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Fallo simulado del proveedor.");
}
