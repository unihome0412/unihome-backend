namespace UniHome.IntegrationTests.TestSupport;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CloudinaryIntegrationFactAttribute : FactAttribute
{
    public CloudinaryIntegrationFactAttribute()
    {
        if (!CloudinaryIntegrationEnvironment.IsConfigured)
        {
            Skip = CloudinaryIntegrationEnvironment.SkipReason;
        }
    }
}
