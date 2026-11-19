namespace UniHome.Application.OccupancyLinks.Abstractions;

public interface IUnitInvitationCodeGenerator
{
    /// <summary>Genera un código de invitación legible, sin caracteres ambiguos.</summary>
    string Generate();
}
