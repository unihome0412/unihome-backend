namespace UniHome.Application.Identity.Exceptions;

public sealed class AccountNotFoundException : Exception
{
    public AccountNotFoundException()
        : base("La cuenta no existe.")
    {
    }
}
