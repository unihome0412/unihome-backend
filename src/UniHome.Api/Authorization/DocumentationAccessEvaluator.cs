using System.Globalization;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;

namespace UniHome.Api.Authorization;

/// <summary>
/// Decide si una cuenta puede consultar la documentación. Un administrador
/// conserva acceso; un lector documental solo recibe ese permiso de lectura.
/// </summary>
public sealed class DocumentationAccessEvaluator
{
    private readonly IOptions<AdminAccessOptions> _admin;
    private readonly IOptions<DocumentationAccessOptions> _documentation;

    public DocumentationAccessEvaluator(
        IOptions<AdminAccessOptions> admin,
        IOptions<DocumentationAccessOptions> documentation)
    {
        _admin = admin;
        _documentation = documentation;
    }

    public bool IsAllowed(Guid accountId)
    {
        var subject = accountId.ToString("N", CultureInfo.InvariantCulture);
        return _admin.Value.IsAdmin(subject) || _documentation.Value.IsReader(subject);
    }

    public bool IsAllowed(string? accountId) =>
        Guid.TryParseExact(accountId, "N", out var parsed) && IsAllowed(parsed);
}
