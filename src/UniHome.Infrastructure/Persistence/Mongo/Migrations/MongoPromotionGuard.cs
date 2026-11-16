namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Autorización independiente de la migración ordinaria. No consulta
/// UNIHOME_MIGRATION_ALLOWED_DATABASES: una allowlist normal no puede habilitar
/// escrituras en un destino de promoción.
/// </summary>
public static class MongoPromotionGuard
{
    public const string TargetDatabaseName = "unihome_desarrollo";
    public const string VerificationTargetDatabaseName = "unihome_verificacion";
    public const string AuthorizationTargetEnvironmentVariable =
        "UNIHOME_PROMOTION_AUTHORIZED_DATABASE";
    public const int MaximumApprovalReferenceLength = 128;

    /// <summary>
    /// Destinos fijos. La CLI no puede introducir un nombre arbitrario: además del
    /// destino exacto se exige la variable de autorización con el mismo valor.
    /// </summary>
    private static readonly HashSet<string> AllowedTargets = new(StringComparer.Ordinal)
    {
        TargetDatabaseName,
        VerificationTargetDatabaseName
    };

    public static bool IsAllowedTarget(string? databaseName) =>
        !string.IsNullOrWhiteSpace(databaseName) &&
        AllowedTargets.Contains(databaseName);

    public static MongoPromotionAuthorization EnsureAuthorized(
        string? databaseName,
        string? confirmationDatabase,
        string? authorizedDatabase,
        string? approvalReference)
    {
        if (!IsAllowedTarget(databaseName))
        {
            throw new MongoPromotionAuthorizationException(
                "El destino de promoción no está en la lista fija de destinos autorizados.");
        }

        if (!string.Equals(confirmationDatabase, databaseName, StringComparison.Ordinal))
        {
            throw new MongoPromotionAuthorizationException(
                "La confirmación del destino no coincide exactamente.");
        }

        if (!string.Equals(authorizedDatabase, databaseName, StringComparison.Ordinal))
        {
            throw new MongoPromotionAuthorizationException(
                "Falta la autorización específica para el destino de promoción.");
        }

        if (string.IsNullOrWhiteSpace(approvalReference) ||
            approvalReference.Length > MaximumApprovalReferenceLength ||
            approvalReference.Any(char.IsControl))
        {
            throw new MongoPromotionAuthorizationException(
                "La referencia de aprobación no es válida.");
        }

        return new MongoPromotionAuthorization(
            databaseName!,
            confirmationDatabase!,
            approvalReference,
            authorizedDatabase!);
    }
}

public sealed record MongoPromotionAuthorization(
    string DatabaseName,
    string ConfirmationDatabase,
    string ApprovalReference,
    string AuthorizedDatabase)
{
    public string Version { get; init; } = MongoIdentityMigration.Version;
}

public sealed class MongoPromotionAuthorizationException : InvalidOperationException
{
    public MongoPromotionAuthorizationException(string message)
        : base(message)
    {
    }
}
