using Microsoft.Extensions.Configuration;
using UniHome.MongoMigrator;

namespace UniHome.UnitTests.Tools;

public sealed class MongoMigratorConfigurationTests
{
    [Theory]
    [InlineData(null, 5)]
    [InlineData("", 5)]
    [InlineData("invalid", 5)]
    [InlineData("0", 5)]
    [InlineData("-1", 5)]
    [InlineData("61", 5)]
    [InlineData("2147483648", 5)]
    [InlineData("1", 1)]
    [InlineData("15", 15)]
    [InlineData("60", 60)]
    public void Timeout_DebeConservarRangoYFallbackActual(string? raw, int expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["MongoDb:ServerSelectionTimeoutSeconds"] = raw }).Build();
        Assert.Equal(expected, MongoMigratorConfiguration.GetTimeoutSeconds(configuration));
    }

    [Fact]
    public void Entorno_DebeNormalizarDobleGuionBajoYTenerPrioridadSobreProveedorAnterior()
    {
        // Prefijo único: no toca MongoDb__ConnectionString real ni User Secrets.
        var prefix = $"UNIHOME_UNIT_{Guid.NewGuid():N}_";
        var variable = prefix + "MongoDb__ConnectionString";
        Environment.SetEnvironmentVariable(variable, "synthetic-environment-value");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDb:ConnectionString"] = "synthetic-prior-provider"
                })
                .AddEnvironmentVariables(prefix)
                .Build();
            Assert.Equal("synthetic-environment-value", MongoMigratorConfiguration.GetConnectionString(configuration));
            Assert.Null(configuration["MongoDb__ConnectionString"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CredencialAusenteONula_DebeRechazarseSinValoresSensibles(string? canonicalValue)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = canonicalValue
            }).Build();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MongoMigratorConfiguration.GetConnectionString(configuration));
        Assert.DoesNotContain("synthetic-other-credential", exception.Message);
    }

    [Fact]
    public void ClaveLiteralHeredada_DebeMantenerFallbackExistente()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = " ",
                ["MongoDb__ConnectionString"] = "synthetic-legacy-value"
            }).Build();
        Assert.Equal("synthetic-legacy-value", MongoMigratorConfiguration.GetConnectionString(configuration));
    }

    [Fact]
    public void TimeoutLiteralHeredado_DebeMantenerFallbackExistente()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["MongoDb__ServerSelectionTimeoutSeconds"] = "20" }).Build();
        Assert.Equal(20, MongoMigratorConfiguration.GetTimeoutSeconds(configuration));
    }
}
