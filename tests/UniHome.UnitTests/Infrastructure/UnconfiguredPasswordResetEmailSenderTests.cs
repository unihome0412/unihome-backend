using Microsoft.Extensions.Logging.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class UnconfiguredPasswordResetEmailSenderTests
{
    [Fact]
    public async Task SendAsync_SinProveedor_DevuelveFalso()
    {
        var sender = new UnconfiguredPasswordResetEmailSender(
            NullLogger<UnconfiguredPasswordResetEmailSender>.Instance);

        var sent = await sender.SendAsync(
            EmailAddress.Create("ana@uni.edu"),
            "token-de-prueba");

        Assert.False(sent);
    }
}
