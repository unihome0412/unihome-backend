using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Universities;

namespace UniHome.Application.Profiles.GetProfile;

public enum AccountLabel
{
    UsuarioUniHome,
    PublicacionEnRevision,
    ConEspaciosPublicados
}

/// <summary>
/// Vista de solo lectura del perfil M02. Los indicadores derivados se calculan en
/// servidor; ninguna etiqueta se persiste en la cuenta. <see cref="CurrentUniversity"/>
/// es null hasta que la cuenta la fije por primera vez (HU-087).
/// </summary>
public sealed record ProfileView(
    Account Account,
    AccountLabel Label,
    bool IsLinkedToUnit,
    bool CanPublish,
    PublicationState? FirstPublicationState,
    University? CurrentUniversity);
