using UniHome.Application.Publications.SubmitFirstPublication;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Properties;

namespace UniHome.Api.Contracts.Publications;

public sealed record PublicacionResponse(
    Guid Id,
    PublicationState Estado,
    DateTimeOffset FechaEnvio)
{
    public static PublicacionResponse FromView(FirstPublicationSubmissionView view) =>
        new(view.PublicacionId, view.Estado, view.FechaEnvio);
}
