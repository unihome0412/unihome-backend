namespace UniHome.Api.Configuration;

/// <summary>
/// Privilegio administrativo interno (ARQ-004/M13). No se acepta un rol enviado
/// por el cliente ni se incluye en el token: la API revalida el claim <c>sub</c>
/// del JWT contra esta lista configurada en el servidor. Una lista vacía deniega
/// todas las rutas administrativas.
/// </summary>
public sealed class AdminAccessOptions
{
    public const string SectionName = "Admin";
    public const string PolicyName = "Admin";

    private string[] _accountIds = [];

    public string[] AccountIds
    {
        get => _accountIds;
        set => _accountIds = value ?? [];
    }

    public bool IsAdmin(string? accountId)
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
