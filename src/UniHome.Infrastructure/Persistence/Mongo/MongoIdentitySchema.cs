namespace UniHome.Infrastructure.Persistence.Mongo;

/// <summary>
/// Nombres físicos compartidos por los documentos, los índices y la migración de
/// identidad M01. Evita que la migración y los adaptadores diverjan al renombrar
/// un campo o un índice.
/// </summary>
public static class MongoIdentitySchema
{
    public const string AccountsCollection = "cuentas";
    public const string SessionsCollection = "sesiones_refresh";
    public const string AccountRecoveryCollection = "restablecimientos_contrasena";

    public const string AccountsNormalizedEmailField = "correoNormalizado";
    public const string SessionsAccountIdField = "cuentaId";
    public const string SessionsCurrentTokenHashField = "hashRefreshActual";
    public const string SessionsExpiresAtUtcField = "expiraEnUtc";
    public const string AccountRecoveryAccountIdField = "accountId";
    public const string AccountRecoveryTokenHashField = "tokenHash";
    public const string AccountRecoveryExpiresAtUtcField = "expiresAt";

    public const string AccountsUniqueEmailIndex = "ux_cuentas_correo_normalizado";
    public const string SessionsUniqueCurrentTokenIndex = "ux_sesiones_refresh_token_actual";
    public const string SessionsAccountIndex = "ix_sesiones_refresh_cuenta";
    public const string SessionsExpirationTtlIndex = "ttl_sesiones_refresh_expiracion";
    public const string AccountRecoveryUniqueTokenIndex = "ux_restablecimientos_token";
    public const string AccountRecoveryAccountIndex = "ix_restablecimientos_cuenta";
    public const string AccountRecoveryExpirationTtlIndex = "ttl_restablecimientos_expires";

    public const string MigrationLedgerCollection = "migraciones";
}
