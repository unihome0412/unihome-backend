using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Common;
using UniHome.Application.Identity.Admin;

namespace UniHome.Api.Contracts.Admin;

public sealed class BuscarCuentasQuery
{
    [FromQuery(Name = "buscar")]
    public string? Buscar { get; set; }

    [ModelBinder(BinderType = typeof(EnumQueryModelBinder<AccountSearchFilter>))]
    [FromQuery(Name = "filtro")]
    public AccountSearchFilter? Filtro { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = SearchAccountsService.DefaultPageSize;
}
