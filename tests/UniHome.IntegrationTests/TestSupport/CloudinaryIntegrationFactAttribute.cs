namespace UniHome.IntegrationTests.TestSupport;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CloudinaryIntegrationFactAttribute : FactAttribute
{
    public CloudinaryIntegrationFactAttribute()
    {
        // Sin la bandera explícita la prueba queda PENDIENTE. Si la bandera está
        // activa, no se omite: RequireConfiguration falla si faltan credenciales.
        if (!CloudinaryIntegrationEnvironment.IsRunRequested)
        {
            Skip = CloudinaryIntegrationEnvironment.SkipReason;
        }
    }
}
