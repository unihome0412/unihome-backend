using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Identity;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Crea cuentas persistidas directamente para probar login y refresh sin depender
/// de un endpoint de confirmación aún no expuesto.
/// </summary>
public static class TestAccountFactory
{
    public const string DefaultPassword = "ClaveSegura1";

    private static readonly AspNetCorePasswordHasher Hasher = new();

    public static Account Create(
        string email,
        string password = DefaultPassword,
        AccountStatus status = AccountStatus.Active,
        bool emailConfirmed = true,
        Guid? id = null,
        DateOnly? birthDate = null)
    {
        var now = DateTimeOffset.UtcNow;

        return Account.Restore(
            id ?? Guid.NewGuid(),
            "Ana López",
            EmailAddress.Create(email),
            birthDate ?? new DateOnly(2004, 5, 10),
            Gender.Mujer,
            Hasher.HashPassword(password),
            new PrivacyAcceptance("2026-01", now),
            status,
            emailConfirmed,
            now,
            emailConfirmed ? now : null,
            emailVerification: null,
            verificationResendAttempts: []);
    }

    public static Account WithStatus(Account account, AccountStatus status) =>
        Account.Restore(
            account.Id,
            account.FullName,
            account.Email,
            account.BirthDate,
            account.Gender,
            account.PasswordHash,
            account.PrivacyAcceptance,
            status,
            account.IsEmailConfirmed,
            account.CreatedAt,
            account.EmailConfirmedAt,
            account.EmailVerification,
            account.VerificationResendAttempts);
}
