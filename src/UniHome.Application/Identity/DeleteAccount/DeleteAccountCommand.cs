namespace UniHome.Application.Identity.DeleteAccount;

public sealed record DeleteAccountCommand(
    Guid AccountId,
    string? Password,
    bool ConfirmedDataLoss);
