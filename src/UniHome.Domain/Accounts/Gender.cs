namespace UniHome.Domain.Accounts;

/// <summary>
/// Género declarado en el registro. Los nombres coinciden con el contrato público
/// de HU-001 para no introducir traducciones distintas entre API y dominio.
/// </summary>
public enum Gender
{
    Mujer,
    Hombre,
    PrefiereNoDecir
}
