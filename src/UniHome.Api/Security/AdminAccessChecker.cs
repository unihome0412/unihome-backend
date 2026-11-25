using System.Globalization;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Api.Security;

/// <summary>Adapta <see cref="AdminAccessOptions"/> (Api) al puerto de Application.</summary>
public sealed class AdminAccessChecker : IAdminAccessChecker
{
    private readonly IOptions<AdminAccessOptions> _options;

    public AdminAccessChecker(IOptions<AdminAccessOptions> options)
    {
        _options = options;
    }

    public bool IsAdmin(Guid accountId) =>
        _options.Value.IsAdmin(accountId.ToString("N", CultureInfo.InvariantCulture));
}
