namespace UniHome.Domain.Accounts;

public enum EmailConfirmationResult
{
    Confirmed,
    AlreadyConfirmed,
    NotIssued,
    IncorrectCode,
    ExpiredCode,
    AlreadyUsed
}
