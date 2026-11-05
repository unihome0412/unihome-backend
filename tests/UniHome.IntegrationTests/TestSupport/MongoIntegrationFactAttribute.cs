namespace UniHome.IntegrationTests.TestSupport;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MongoIntegrationFactAttribute : FactAttribute
{
    public MongoIntegrationFactAttribute()
    {
        if (!MongoIntegrationEnvironment.IsConfigured)
        {
            Skip = MongoIntegrationEnvironment.SkipReason;
        }
    }
}
