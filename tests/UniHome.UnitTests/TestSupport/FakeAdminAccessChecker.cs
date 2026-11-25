using UniHome.Application.Identity.Abstractions;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeAdminAccessChecker : IAdminAccessChecker
{
    public HashSet<Guid> AdminAccountIds { get; } = [];

    public bool IsAdmin(Guid accountId) => AdminAccountIds.Contains(accountId);
}
