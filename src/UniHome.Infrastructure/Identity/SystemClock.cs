using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Identity;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
