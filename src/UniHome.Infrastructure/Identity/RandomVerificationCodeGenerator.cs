using System.Globalization;
using System.Security.Cryptography;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Identity;

public sealed class RandomVerificationCodeGenerator : IVerificationCodeGenerator
{
    private const int UpperBound = 1_000_000;

    public string Generate() =>
        RandomNumberGenerator
            .GetInt32(0, UpperBound)
            .ToString("D6", CultureInfo.InvariantCulture);
}
