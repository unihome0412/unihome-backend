using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Abstractions;

public interface IInformativePaymentRepository
{
    Task SaveAsync(InformativePayment payment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InformativePayment>> ListByRentPeriodAsync(
        Guid rentPeriodId,
        CancellationToken cancellationToken = default);
}
