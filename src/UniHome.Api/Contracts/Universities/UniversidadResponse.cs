using UniHome.Domain.Universities;

namespace UniHome.Api.Contracts.Universities;

/// <summary>Contrato administrativo de universidad para EP-M03-02.</summary>
public sealed record UniversidadResponse(
    Guid Id,
    string NombreYCampus,
    bool Activa,
    DateTimeOffset FechaAlta)
{
    public static UniversidadResponse FromDomain(University university) =>
        new(
            university.Id,
            university.NameAndCampus,
            university.IsActive,
            university.CreatedAt);
}
