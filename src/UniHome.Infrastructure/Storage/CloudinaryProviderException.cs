namespace UniHome.Infrastructure.Storage;

/// <summary>Fallo controlado del proveedor Cloudinary; nunca incluye credenciales.</summary>
internal sealed class CloudinaryProviderException : Exception
{
    public CloudinaryProviderException()
        : base("El proveedor de fotografías no pudo completar la operación.")
    {
    }

    public CloudinaryProviderException(Exception innerException)
        : base("El proveedor de fotografías no pudo completar la operación.", innerException)
    {
    }
}
