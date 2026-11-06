namespace UniHome.MongoMigrator.Diagnostics;

public enum MongoExecutionStage
{
    ConfigurationLoading,
    ConfigurationValidation,
    SettingsParsing,
    ClientConstruction,
    DatabaseSelection,
    MongoCommandExecution,
    ConnectionOpening,
    ServerSelection,
    TlsNegotiation,
    MongoAuthentication,
    ClientDisposal,
    Promotion
}
