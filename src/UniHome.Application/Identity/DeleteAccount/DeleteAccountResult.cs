namespace UniHome.Application.Identity.DeleteAccount;

public enum DeleteAccountStatus
{
    Deleted,
    IncorrectPassword,
    ConfirmationMissing,
    AccountNotFound,

    /// <summary>HU-096: la cuenta tiene un vínculo de ocupación activo (M09), como inquilino.</summary>
    ActiveOccupancyLinks,

    /// <summary>HU-096: una propiedad del titular tiene inquilinos activos (M09).</summary>
    PropertiesWithTenants,

    /// <summary>HU-096: un administrador no puede eliminar su propia cuenta.</summary>
    AdminCannotSelfDelete
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

    public static DeleteAccountResult ActiveOccupancyLinks() =>
        new(DeleteAccountStatus.ActiveOccupancyLinks);

    public static DeleteAccountResult PropertiesWithTenants() =>
        new(DeleteAccountStatus.PropertiesWithTenants);

    public static DeleteAccountResult AdminCannotSelfDelete() =>
        new(DeleteAccountStatus.AdminCannotSelfDelete);
}
