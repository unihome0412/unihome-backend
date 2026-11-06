using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.RegisterAccount;

/// <summary>
/// Datos de HU-001/HU-012. Se reciben como nullable para que Application pueda
/// devolver errores por campo sin depender de las validaciones del transporte.
/// </summary>
public sealed record RegisterAccountCommand(
    string? FullName,
    string? Email,
    DateOnly? BirthDate,
    Gender? Gender,
    string? Password,
    string? ConfirmPassword,
    bool AcceptedPrivacy,
    string? PrivacyVersion)
{
    // Evita que un log accidental del comando exponga contraseñas o datos personales.
    public override string ToString() =>
        "RegisterAccountCommand { datos personales y credenciales redactados }";
}
