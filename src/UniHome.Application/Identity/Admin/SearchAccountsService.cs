using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles.GetProfile;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;

namespace UniHome.Application.Identity.Admin;

/// <summary>
/// EP-M13-02 (HU-073): búsqueda administrativa de cuentas por nombre o
/// correo, con filtro por categoría y paginación. La categoría reutiliza
/// exactamente la misma etiqueta que ve el propio titular en su perfil
/// (<see cref="AccountLabel"/>, HU-002/008/009): sin unidades propias
/// ("Buscan alojamiento"), con al menos una aprobada ("Con publicaciones") o
/// con al menos una en revisión y ninguna aprobada ("En revisión"). La
/// categoría depende de las unidades de cada cuenta, así que la paginación
/// se aplica después de calcularla, no en el repositorio.
/// </summary>
public sealed class SearchAccountsService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 100_000;

    private readonly IAccountRepository _accounts;
    private readonly IUnitRepository _units;

    public SearchAccountsService(IAccountRepository accounts, IUnitRepository units)
    {
        _accounts = accounts;
        _units = units;
    }

    public async Task<OperationResult<AccountSearchPageView>> SearchAsync(
        string? query,
        AccountSearchFilter filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > MaxPage)
        {
            return OperationResult<AccountSearchPageView>.ValidationFailed(
                "page", $"La página debe estar entre 1 y {MaxPage}.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return OperationResult<AccountSearchPageView>.ValidationFailed(
                "pageSize", $"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
        }

        var candidates = await _accounts.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        var matches = new List<AccountSearchItemView>(candidates.Count);

        foreach (var account in candidates)
        {
            var units = await _units.ListByOwnerAsync(account.Id, cancellationToken).ConfigureAwait(false);
            var label = ComputeLabel(units);

            if (filtro != AccountSearchFilter.Todos && MapFilter(filtro) != label)
            {
                continue;
            }

            matches.Add(new AccountSearchItemView(account, label));
        }

        var total = matches.Count;
        var skip = checked((page - 1) * pageSize);
        var pageItems = matches.Skip(skip).Take(pageSize).ToArray();

        return OperationResult<AccountSearchPageView>.Success(new AccountSearchPageView(pageItems, total));
    }

    private static AccountLabel ComputeLabel(IReadOnlyList<Domain.Units.Unit> units) =>
        units.Any(unit => unit.PublicationState == PublicationState.Aprobada)
            ? AccountLabel.ConEspaciosPublicados
            : units.Any(unit => unit.PublicationState == PublicationState.EnRevision)
                ? AccountLabel.PublicacionEnRevision
                : AccountLabel.UsuarioUniHome;

    private static AccountLabel MapFilter(AccountSearchFilter filtro) => filtro switch
    {
        AccountSearchFilter.BuscanAlojamiento => AccountLabel.UsuarioUniHome,
        AccountSearchFilter.ConPublicaciones => AccountLabel.ConEspaciosPublicados,
        AccountSearchFilter.EnRevision => AccountLabel.PublicacionEnRevision,
        _ => throw new ArgumentOutOfRangeException(nameof(filtro))
    };
}

public sealed record AccountSearchItemView(Account Account, AccountLabel Label);

public sealed record AccountSearchPageView(IReadOnlyList<AccountSearchItemView> Items, int Total);
