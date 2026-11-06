namespace UniHome.Application.Identity.Exceptions;

/// <summary>
/// Conflicto de unicidad de correo detectado por el almacenamiento. No incluye
/// el correo en el mensaje para evitar que acabe en registros.
/// </summary>
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("Ya existe una cuenta con el correo indicado.")
    {
    }
}
