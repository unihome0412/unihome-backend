using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryInformativePaymentRepository : IInformativePaymentRepository
{
    private readonly object _gate = new();
    private readonly List<InformativePayment> _payments = [];

    public Task SaveAsync(InformativePayment payment, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _payments.RemoveAll(item => item.Id == payment.Id);
            _payments.Add(payment);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InformativePayment>> ListByRentPeriodAsync(
        Guid rentPeriodId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<InformativePayment> items = _payments
                .Where(payment => payment.RentPeriodId == rentPeriodId)
                .OrderBy(payment => payment.ReceivedOn)
                .ToArray();
            return Task.FromResult(items);
        }
    }
}
