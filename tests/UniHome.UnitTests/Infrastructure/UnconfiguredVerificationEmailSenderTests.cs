using Microsoft.Extensions.Logging.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class UnconfiguredVerificationEmailSenderTests
{
    [Fact]
    public async Task SendAsync_SinProveedor_DevuelveFalso()
    {
        var sender = new UnconfiguredVerificationEmailSender(
            NullLogger<UnconfiguredVerificationEmailSender>.Instance);

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            "123456");

        Assert.False(sent);
    }
}
