namespace UniHome.Application.Identity.Abstractions;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
