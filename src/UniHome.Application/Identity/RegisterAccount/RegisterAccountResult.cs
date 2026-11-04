namespace UniHome.Application.Identity.RegisterAccount;

public sealed record RegisterAccountResult(
    Guid? AccountId,
    string? Email,
    bool EmailAlreadyRegistered,
    bool VerificationEmailSent,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    public bool Succeeded => AccountId.HasValue;

    // El correo no debe acabar en un log si alguien imprime el resultado.
    public override string ToString() =>
        $"RegisterAccountResult {{ Succeeded = {Succeeded}, EmailAlreadyRegistered = {EmailAlreadyRegistered} }}";

    public static RegisterAccountResult Success(
        Guid accountId,
        string email,
        bool verificationEmailSent) =>
        new(accountId, email, EmailAlreadyRegistered: false, verificationEmailSent, NoErrors);

    public static RegisterAccountResult DuplicateEmail() =>
        new(null, null, EmailAlreadyRegistered: true, VerificationEmailSent: false, NoErrors);

    public static RegisterAccountResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, null, EmailAlreadyRegistered: false, VerificationEmailSent: false, errors);
}
