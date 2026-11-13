using UniHome.Domain.Universities;

namespace UniHome.Api.Contracts.Universities;

/// <summary>Contrato público de autocompletado para EP-M03-01.</summary>
public sealed record UniversidadResumenResponse(Guid Id, string NombreYCampus)
{
    public static UniversidadResumenResponse FromDomain(University university) =>
        new(university.Id, university.NameAndCampus);
}
