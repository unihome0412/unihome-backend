using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Identity;

internal sealed class JwtTokenSettings : IAuthTokenSettings
{
    private readonly IOptions<JwtOptions> _options;

    public JwtTokenSettings(IOptions<JwtOptions> options)
    {
        _options = options;
    }

    public TimeSpan RefreshTokenLifetime => _options.Value.RefreshTokenLifetime;
}
