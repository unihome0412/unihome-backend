using UniHome.Domain.Accounts;

namespace UniHome.Api.Contracts.Auth;

/// <summary>
/// Contrato de EP-M01-01 (HU-001/HU-012). Los campos son nullable para que la
/// validación de negocio devuelva errores por campo en español.
/// </summary>
public sealed class RegistroRequest
{
    public string? NombreCompleto { get; init; }

    public string? Correo { get; init; }

    public DateOnly? FechaNacimiento { get; init; }

    public Gender? Genero { get; init; }

    public string? Contrasena { get; init; }

    public string? ConfirmarContrasena { get; init; }

    public bool AceptoAvisoPrivacidad { get; init; }

    public string? VersionAvisoPrivacidad { get; init; }
}
