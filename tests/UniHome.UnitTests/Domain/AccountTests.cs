using System.Text;
using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.Domain;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_CreaCuentaActivaSinConfirmarYGuardaPrivacidad()
    {
        var account = CreateAccount();

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Ana López", account.FullName);
        Assert.Equal("ana@uni.edu", account.Email.Value);
        Assert.Equal(new DateOnly(2004, 5, 10), account.BirthDate);
        Assert.Equal(Gender.Mujer, account.Gender);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.False(account.IsEmailConfirmed);
        Assert.Null(account.EmailConfirmedAt);
        Assert.Equal("2026-01", account.PrivacyAcceptance.Version);
        Assert.Equal(Now, account.PrivacyAcceptance.AcceptedAt);
        Assert.Null(account.EmailVerification);
    }

    [Fact]
    public void Register_RechazaFechaNacimientoFutura()
    {
        var future = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1);

        Assert.Throws<ArgumentException>(() =>
            Account.Register(
                Guid.NewGuid(),
                "Ana López",
                EmailAddress.Create("ana@uni.edu"),
                future,
                Gender.Mujer,
                "hash",
                new PrivacyAcceptance("2026-01", Now),
                Now));
    }

    [Fact]
    public void Register_RechazaNombreFueraDeRango()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.Register(
                Guid.NewGuid(),
                "A",
                EmailAddress.Create("ana@uni.edu"),
                new DateOnly(2004, 5, 10),
                Gender.Mujer,
                "hash",
                new PrivacyAcceptance("2026-01", Now),
                Now));
    }

    [Fact]
    public void Register_RechazaCaracteresDeControlEnElNombre()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.Register(
                Guid.NewGuid(),
                "Ana\u0000López",
                EmailAddress.Create("ana@uni.edu"),
                new DateOnly(2004, 5, 10),
                Gender.Mujer,
                "hash",
                new PrivacyAcceptance("2026-01", Now),
                Now));
    }

    [Fact]
    public void PrivacyAcceptance_RechazaVersionDemasiadoLargaOConControles()
    {
        Assert.Throws<ArgumentException>(() =>
            new PrivacyAcceptance(new string('v', PrivacyAcceptance.MaxVersionLength + 1), Now));

        Assert.Throws<ArgumentException>(() =>
            new PrivacyAcceptance("2026\u0000-01", Now));
    }

    [Fact]
    public void ConfirmEmail_ConsumeCodigoYConfirmaDentroDeVigencia()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("123456"), Now);

        var result = account.ConfirmEmail(Hash("123456"), Now.AddMinutes(9));

        Assert.Equal(EmailConfirmationResult.Confirmed, result);
        Assert.True(account.IsEmailConfirmed);
        Assert.Equal(Now.AddMinutes(9), account.EmailConfirmedAt);
        Assert.True(account.EmailVerification!.IsConsumed);
    }

    [Fact]
    public void ConfirmEmail_RechazaCodigoVencidoSinConfirmar()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("123456"), Now);

        var result = account.ConfirmEmail(Hash("123456"), Now.AddMinutes(11));

        Assert.Equal(EmailConfirmationResult.ExpiredCode, result);
        Assert.False(account.IsEmailConfirmed);
    }

    [Fact]
    public void ConfirmEmail_RechazaCodigoIncorrecto()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("123456"), Now);

        var result = account.ConfirmEmail(Hash("654321"), Now.AddMinutes(1));

        Assert.Equal(EmailConfirmationResult.IncorrectCode, result);
        Assert.False(account.IsEmailConfirmed);
    }

    [Fact]
    public void ConfirmEmail_NoPermiteReutilizarElCodigoTrasConfirmar()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("123456"), Now);
        account.ConfirmEmail(Hash("123456"), Now.AddMinutes(1));

        var result = account.ConfirmEmail(Hash("123456"), Now.AddMinutes(2));

        Assert.Equal(EmailConfirmationResult.AlreadyConfirmed, result);
    }

    [Fact]
    public void Reenvio_AplicaLimiteDeTresPorVentana()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("inicial"), Now);

        account.ResendEmailVerification(Hash("uno"), Now.AddSeconds(10));
        account.ResendEmailVerification(Hash("dos"), Now.AddSeconds(20));
        account.ResendEmailVerification(Hash("tres"), Now.AddSeconds(30));

        Assert.False(account.CanResendEmailVerification(Now.AddSeconds(40)));
        Assert.Throws<InvalidOperationException>(() =>
            account.ResendEmailVerification(Hash("cuatro"), Now.AddSeconds(40)));
    }

    [Fact]
    public void Reenvio_SePermiteDeNuevoAlPasarLaVentana()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("inicial"), Now);

        account.ResendEmailVerification(Hash("uno"), Now.AddSeconds(10));
        account.ResendEmailVerification(Hash("dos"), Now.AddSeconds(20));
        account.ResendEmailVerification(Hash("tres"), Now.AddSeconds(30));

        Assert.True(account.CanResendEmailVerification(Now.AddMinutes(2)));
    }

    [Fact]
    public void ConfirmEmail_UsaSoloElUltimoCodigoEmitido()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("inicial"), Now);
        account.ResendEmailVerification(Hash("nuevo"), Now.AddSeconds(30));

        var result = account.ConfirmEmail(Hash("inicial"), Now.AddMinutes(1));

        Assert.Equal(EmailConfirmationResult.IncorrectCode, result);
    }

    [Fact]
    public void ConfirmEmail_TrasAgotarIntentos_SeTrataComoVencido()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("123456"), Now);

        for (var attempt = 0; attempt < VerificationCode.MaxConfirmationAttempts; attempt++)
        {
            var incorrect = account.ConfirmEmail(Hash("000000"), Now.AddSeconds(attempt));
            Assert.Equal(EmailConfirmationResult.IncorrectCode, incorrect);
        }

        // El código correcto ya no se acepta: el límite "por cuenta" lo bloqueó.
        var afterLimit = account.ConfirmEmail(Hash("123456"), Now.AddSeconds(100));

        Assert.Equal(EmailConfirmationResult.ExpiredCode, afterLimit);
        Assert.False(account.IsEmailConfirmed);
        Assert.Equal(VerificationCode.MaxConfirmationAttempts, account.EmailVerification!.FailedAttempts);
    }

    [Fact]
    public void ConfirmEmail_IntentosPorDebajoDelLimite_NoBloqueaElCodigoCorrecto()
    {
        var account = CreateAccount();
        account.IssueEmailVerification(Hash("123456"), Now);

        for (var attempt = 0; attempt < VerificationCode.MaxConfirmationAttempts - 1; attempt++)
        {
            account.ConfirmEmail(Hash("000000"), Now.AddSeconds(attempt));
        }

        var result = account.ConfirmEmail(Hash("123456"), Now.AddSeconds(50));

        Assert.Equal(EmailConfirmationResult.Confirmed, result);
        Assert.True(account.IsEmailConfirmed);
    }

    [Fact]
    public void Delete_CambiaEstadoYRegistraFecha()
    {
        var account = CreateAccount();

        account.Delete(Now.AddDays(1));

        Assert.Equal(AccountStatus.Deleted, account.Status);
        Assert.Equal(Now.AddDays(1), account.DeletedAt);
    }

    [Fact]
    public void Delete_SobreUnaCuentaYaEliminada_Lanza()
    {
        var account = CreateAccount();
        account.Delete(Now);

        Assert.Throws<InvalidOperationException>(() => account.Delete(Now.AddMinutes(1)));
    }

    private static Account CreateAccount() =>
        Account.Register(
            Guid.NewGuid(),
            "  Ana López  ",
            EmailAddress.Create("Ana@Uni.EDU"),
            new DateOnly(2004, 5, 10),
            Gender.Mujer,
            "hash:ClaveSegura1",
            new PrivacyAcceptance(" 2026-01 ", Now),
            Now);

    private static string Hash(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"hash:{value}"));
}
