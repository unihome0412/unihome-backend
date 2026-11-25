using System.Text.Json.Serialization;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record ConceptoInput(
    RentConceptName Nombre,
    [property: JsonRequired] bool EsFijo);

public sealed record ConfigurarRentaRequest(
    [property: JsonRequired] decimal RentaTotalMxn,
    [property: JsonRequired] int DiaCorte,
    IReadOnlyList<ConceptoInput>? Conceptos);
