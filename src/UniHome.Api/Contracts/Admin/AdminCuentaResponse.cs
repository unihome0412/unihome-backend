using UniHome.Application.Identity.Admin;
using UniHome.Domain.Accounts;

namespace UniHome.Api.Contracts.Admin;

public sealed record AdminCuentaResponse(
    Guid Id,
    string NombreCompleto,
    string Correo,
    AccountStatus Estado,
    AccountSearchFilter? Etiqueta,
    DateTimeOffset CreadaEn)
{
    public static AdminCuentaResponse FromDomain(Account account) =>
        new(account.Id, account.FullName, account.Email.Value, account.Status, Etiqueta: null, account.CreatedAt);

    public static AdminCuentaResponse FromItem(AccountSearchItemView item) =>
        new(
            item.Account.Id,
            item.Account.FullName,
            item.Account.Email.Value,
            item.Account.Status,
            item.Label switch
            {
                UniHome.Application.Profiles.GetProfile.AccountLabel.ConEspaciosPublicados =>
                    AccountSearchFilter.ConPublicaciones,
                UniHome.Application.Profiles.GetProfile.AccountLabel.PublicacionEnRevision =>
                    AccountSearchFilter.EnRevision,
                _ => AccountSearchFilter.BuscanAlojamiento
            },
            item.Account.CreatedAt);
}
