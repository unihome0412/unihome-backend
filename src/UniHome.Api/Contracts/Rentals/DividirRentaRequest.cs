using System.Text.Json.Serialization;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record CuotaInput(Guid InquilinoAccountId, decimal MontoMxn);

public sealed record DividirRentaRequest(
    [property: JsonRequired] RentSplitMethod Metodo,
    IReadOnlyList<CuotaInput>? Cuotas);
