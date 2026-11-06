namespace UniHome.Application.Identity.Exceptions;

/// <summary>
/// El almacenamiento no está configurado o no respondió. El mensaje no debe
/// contener cadenas de conexión, nombres de servidor ni datos personales.
/// </summary>
public sealed class StorageUnavailableException : Exception
{
    public StorageUnavailableException()
        : base("El almacenamiento no está disponible.")
    {
    }
}
