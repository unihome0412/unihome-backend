namespace UniHome.Application.Identity.DeleteAccount;

public enum DeleteAccountStatus
{
    Deleted,
    IncorrectPassword,
    ConfirmationMissing,
    AccountNotFound
}

public sealed class DeleteAccountResult
{
    private DeleteAccountResult(DeleteAccountStatus status)
    {
        Status = status;
    }

    public DeleteAccountStatus Status { get; }

    public bool Succeeded => Status == DeleteAccountStatus.Deleted;

    public static DeleteAccountResult Deleted() => new(DeleteAccountStatus.Deleted);

    public static DeleteAccountResult IncorrectPassword() =>
        new(DeleteAccountStatus.IncorrectPassword);

    public static DeleteAccountResult ConfirmationMissing() =>
        new(DeleteAccountStatus.ConfirmationMissing);

    public static DeleteAccountResult AccountNotFound() =>
        new(DeleteAccountStatus.AccountNotFound);
}
