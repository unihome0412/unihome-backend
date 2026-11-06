namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Opciones tipadas de MongoDB. Se enlazan desde la sección "MongoDb".
/// La API puede iniciar sin valores configurados; en ese caso readiness
/// reporta la dependencia como no configurada, nunca como sana.
/// </summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";
    public const int DefaultServerSelectionTimeoutSeconds = 5;

    private const int MaxServerSelectionTimeoutSeconds = 60;

    public string? ConnectionString { get; set; }

    public string? DatabaseName { get; set; }

    public int ServerSelectionTimeoutSeconds { get; set; } = DefaultServerSelectionTimeoutSeconds;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) &&
        !string.IsNullOrWhiteSpace(DatabaseName);

    public TimeSpan GetServerSelectionTimeout()
    {
        var seconds = ServerSelectionTimeoutSeconds is > 0 and <= MaxServerSelectionTimeoutSeconds
            ? ServerSelectionTimeoutSeconds
            : DefaultServerSelectionTimeoutSeconds;

        return TimeSpan.FromSeconds(seconds);
    }
}
