using UniHome.Application.Abstractions.Storage;

namespace UniHome.UnitTests.Application;

public sealed class StorageReadinessTests
{
    [Fact]
    public void Ready_MarcaIsReady()
    {
        var readiness = StorageReadiness.Ready();

        Assert.Equal(StorageReadinessStatus.Ready, readiness.Status);
        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Detail);
    }

    [Fact]
    public void NotConfigured_NoSeConsideraListo()
    {
        var readiness = StorageReadiness.NotConfigured();

        Assert.Equal(StorageReadinessStatus.NotConfigured, readiness.Status);
        Assert.False(readiness.IsReady);
    }

    [Fact]
    public void Unavailable_ConservaElDetalleSeguro()
    {
        var readiness = StorageReadiness.Unavailable("Sin respuesta.");

        Assert.Equal(StorageReadinessStatus.Unavailable, readiness.Status);
        Assert.False(readiness.IsReady);
        Assert.Equal("Sin respuesta.", readiness.Detail);
    }
}
