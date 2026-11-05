using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class MongoTestConfigurationSafetyTests
{
    [Theory]
    [InlineData("mongodb://synthetic-user:synthetic-secret/invalid@localhost:27017")]
    [InlineData("not-a-uri-synthetic-secret")]
    public void UriMalformada_NoDebeExponerCredencialesEnResultados(string syntheticUri)
    {
        // El parseo falla antes de construir un cliente: no accede a MongoDB.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MongoIntegrationEnvironment.CreateClientFromConnectionString(syntheticUri));
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("synthetic-secret", exception.ToString());
        Assert.DoesNotContain("synthetic-user", exception.ToString());
        Assert.DoesNotContain("mongodb://", exception.ToString());
    }
}
