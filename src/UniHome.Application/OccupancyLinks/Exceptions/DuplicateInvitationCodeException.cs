namespace UniHome.Application.OccupancyLinks.Exceptions;

public sealed class DuplicateInvitationCodeException : Exception
{
    public DuplicateInvitationCodeException()
        : base("El código de invitación generado ya existe; se reintentará con otro.")
    {
    }
}
