namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Autorización independiente de la migración ordinaria. No consulta
/// UNIHOME_MIGRATION_ALLOWED_DATABASES: una allowlist normal no puede habilitar
/// escrituras en unihome_desarrollo.
/// </summary>
public static class MongoPromotionGuard
{
    public const string TargetDatabaseName = "unihome_desarrollo";
    public const string AuthorizationTargetEnvironmentVariable =
        "UNIHOME_PROMOTION_AUTHORIZED_DATABASE";
    public const int MaximumApprovalReferenceLength = 128;

    public static bool IsExactTarget(string? databaseName) =>
        string.Equals(databaseName, TargetDatabaseName, StringComparison.Ordinal);

    public static MongoPromotionAuthorization EnsureAuthorized(
        string? databaseName,
        string? confirmationDatabase,
        string? authorizedDatabase,
        string? approvalReference)
    {
        if (!IsExactTarget(databaseName))
        {
            throw new MongoPromotionAuthorizationException(
                "El destino de promoción no está autorizado.");
        }

        if (!string.Equals(confirmationDatabase, databaseName, StringComparison.Ordinal))
        {
            throw new MongoPromotionAuthorizationException(
                "La confirmación del destino no coincide exactamente.");
        }

        if (!IsExactTarget(authorizedDatabase))
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
