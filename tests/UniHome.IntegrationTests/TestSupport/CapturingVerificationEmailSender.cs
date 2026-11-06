using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests.TestSupport;

public sealed record SentVerificationEmail(string Email, string Code);

public sealed class CapturingVerificationEmailSender : IVerificationEmailSender
{
    private readonly object _gate = new();
    private readonly List<SentVerificationEmail> _messages = [];

    public IReadOnlyList<SentVerificationEmail> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public Task<bool> SendAsync(
        EmailAddress email,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrEmpty(code);

        lock (_gate)
        {
            _messages.Add(new SentVerificationEmail(email.Value, code));
        }

        return Task.FromResult(true);
    }
}
