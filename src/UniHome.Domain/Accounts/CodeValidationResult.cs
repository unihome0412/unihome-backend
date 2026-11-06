namespace UniHome.Domain.Accounts;

public enum CodeValidationResult
{
    Valid,
    Incorrect,
    Expired,
    AlreadyUsed
}
