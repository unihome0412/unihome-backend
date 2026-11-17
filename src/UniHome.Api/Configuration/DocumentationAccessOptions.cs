namespace UniHome.Api.Configuration;

/// <summary>
/// Permiso de solo lectura para la documentación interna. Es independiente de
/// <see cref="AdminAccessOptions"/>: un integrante del equipo puede consultar
/// Swagger/OpenAPI sin recibir acceso a /api/v1/admin. Los identificadores se
/// configuran por entorno; una lista vacía no concede acceso.
/// </summary>
public sealed class DocumentationAccessOptions
{
    public const string SectionName = "DocumentationAccess";

    private string[] _accountIds = [];

    public string[] AccountIds
    {
        get => _accountIds;
        set => _accountIds = value ?? [];
    }

    public bool IsReader(string? accountId)
    {
        if (!Guid.TryParseExact(accountId, "N", out var subject))
        {
            return false;
        }

        foreach (var configured in AccountIds)
        {
            if (Guid.TryParse(configured, out var candidate) && candidate == subject)
            {
                return true;
            }
        }

        return false;
    }

    public static bool AreValidAccountIds(string[]? accountIds) =>
        accountIds is null ||
        accountIds.All(value => Guid.TryParse(value, out _));
}
