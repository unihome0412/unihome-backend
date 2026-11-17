using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;

namespace UniHome.Application.Profiles.GetProfile;

public enum AccountLabel
{
    UsuarioUniHome,
    PublicacionEnRevision,
    ConEspaciosPublicados
}

/// <summary>
/// Vista de solo lectura del perfil M02. Los indicadores derivados se calculan en
/// servidor; ninguna etiqueta se persiste en la cuenta.
/// </summary>
public sealed record ProfileView(
    Account Account,
    AccountLabel Label,
    bool IsLinkedToUnit,
    bool CanPublish,
    PublicationState? FirstPublicationState);
