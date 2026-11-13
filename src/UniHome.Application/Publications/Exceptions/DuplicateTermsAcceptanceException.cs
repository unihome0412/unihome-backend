namespace UniHome.Application.Publications.Exceptions;

public sealed class DuplicateTermsAcceptanceException : Exception
{
    public DuplicateTermsAcceptanceException()
        : base("Los términos de esta versión ya fueron aceptados.")
    {
    }
}
