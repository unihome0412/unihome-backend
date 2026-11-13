using UniHome.Api.Contracts.Universities;
using UniHome.Application.Profiles.GetProfile;
using UniHome.Domain.Accounts;

namespace UniHome.Api.Contracts.Profiles;

/// <summary>
/// EP-M02-01 (HU-002/HU-008). Solo datos propios y campos derivados; nunca
/// incluye hash de contraseña, claims administrativos ni identificadores internos.
/// </summary>
public sealed record PerfilResponse(
    string NombreCompleto,
    string Correo,
    DateOnly FechaNacimiento,
    Gender Genero,
    UniversidadResumenResponse? UniversidadActual,
    AccountLabel EtiquetaCuenta,
    bool VinculadoAUnidad,
    bool PuedePublicar);
