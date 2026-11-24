using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record CuotaInput(Guid InquilinoAccountId, decimal MontoMxn);

public sealed record DividirRentaRequest(
    RentSplitMethod Metodo,
    IReadOnlyList<CuotaInput>? Cuotas);
