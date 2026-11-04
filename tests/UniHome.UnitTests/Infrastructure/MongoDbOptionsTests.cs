using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoDbOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_EsFalso_SiFaltaConnectionString(string? connectionString)
    {
        var options = new MongoDbOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "unihome"
        };

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_EsFalso_SiFaltaDatabaseName(string? databaseName)
    {
        var options = new MongoDbOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = databaseName
        };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_EsVerdadero_ConConnectionStringYDatabaseName()
    {
        var options = new MongoDbOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "unihome"
        };

        Assert.True(options.IsConfigured);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(61, 5)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    public void GetServerSelectionTimeout_UsaValorValidoOElPredeterminado(
        int configuredSeconds,
        int expectedSeconds)
    {
        var options = new MongoDbOptions
        {
            ServerSelectionTimeoutSeconds = configuredSeconds
        };

        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            options.GetServerSelectionTimeout());
    }
}
