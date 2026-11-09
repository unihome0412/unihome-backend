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

    [Theory]
    [InlineData("mongodb://127.0.0.1:27017/?directConnection=true")]
    [InlineData("mongodb://127.0.0.1:27017/?directConnection=true&retryWrites=false&serverSelectionTimeoutMS=5000&connectTimeoutMS=5000")]
    [InlineData("mongodb://localhost:27017/?directConnection=true")]
    [InlineData("mongodb://[::1]:27017/?directConnection=true")]
    public void UriLoopbackDirectaSinCredenciales_EsAceptada(string uri)
    {
        var exception = Record.Exception(() =>
            MongoIntegrationEnvironment.EnsureLoopbackOnly(uri));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("mongodb://203.0.113.10:27017/?directConnection=true")]
    [InlineData("mongodb+srv://cluster.example.invalid/?retryWrites=true")]
    [InlineData("mongodb://synthetic-user:synthetic-secret@127.0.0.1:27017/?directConnection=true")]
    [InlineData("mongodb://127.0.0.1:27017")]
    public void UriNoLoopbackDirectaSinCredenciales_EsRechazadaSinFiltrarLaUri(string uri)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MongoIntegrationEnvironment.EnsureLoopbackOnly(uri));

        Assert.DoesNotContain("203.0.113.10", exception.ToString());
        Assert.DoesNotContain("synthetic-secret", exception.ToString());
        Assert.DoesNotContain("synthetic-user", exception.ToString());
        Assert.DoesNotContain("mongodb://", exception.ToString());
    }
}
