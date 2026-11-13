namespace UniHome.Application.Properties.Exceptions;

public sealed class DuplicatePropertyException : Exception
{
    public DuplicatePropertyException()
        : base("Ya existe una propiedad con los mismos datos para esta cuenta.")
    {
    }
}
