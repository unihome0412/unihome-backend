using System.ComponentModel;
using System.Security.Authentication;
using UniHome.MongoMigrator.Diagnostics;

namespace UniHome.UnitTests.Tools;

public sealed class MongoMigratorDiagnosticsTests
{
    [Fact]
    public void Diagnostico_DebeEmitirSoloCamposPermitidosSinLeerMensajeNiToString()
    {
        var exception = new SensitiveException();
        exception.Data["private"] = "synthetic-sensitive-data";
        using var output = new StringWriter();

        new SafeMongoDiagnostics().WriteFailure(output, exception, MongoExecutionStage.SettingsParsing);

        var text = output.ToString();
        Assert.Contains("Stage=SettingsParsing", text);
        Assert.Contains($"ExceptionType={typeof(SensitiveException).FullName}", text);
        Assert.Contains("InnerExceptionType=(none)", text);
        Assert.Contains("HResult=0x80090304", text);
        Assert.DoesNotContain("synthetic-sensitive-data", text);
        AssertOnlyAllowedFields(text);
    }

    [Fact]
    public void TimeoutSdam_DebeConservarCausaTlsObservadaSinImprimirMensajes()
    {
        var native = new Win32Exception(unchecked((int)0x80090304), "synthetic-sensitive-native");
        var tls = new AuthenticationException("synthetic-sensitive-tls", native);
        var connection = new IOException("synthetic-sensitive-connection", tls);
        var diagnostics = new SafeMongoDiagnostics();
        diagnostics.ObserveTransportFailure(connection, MongoExecutionStage.ConnectionOpening);

        using var output = new StringWriter();
        diagnostics.WriteFailure(output, new TimeoutException("synthetic-sensitive-timeout"),
            MongoExecutionStage.MongoCommandExecution);

        var text = output.ToString();
        Assert.Contains("Stage=MongoCommandExecution", text);
        Assert.Contains("Stage=TlsNegotiation", text);
        Assert.Contains("ExceptionType=System.TimeoutException", text);
        Assert.Contains("ExceptionType=System.Security.Authentication.AuthenticationException", text);
        Assert.Contains("InnerExceptionType=System.ComponentModel.Win32Exception", text);
        Assert.Contains($"HResult=0x{unchecked((uint)native.HResult):X8}", text);
        Assert.DoesNotContain("synthetic-sensitive", text);
        AssertOnlyAllowedFields(text);
    }

    [Fact]
    public void CadenaTlsDirecta_DebeIdentificarTlsAdemasDeLaEtapaDelCliente()
    {
        var diagnostics = new SafeMongoDiagnostics();
        using var output = new StringWriter();
        diagnostics.WriteFailure(output, new AuthenticationException("synthetic-secret"),
            MongoExecutionStage.ClientConstruction);
        var text = output.ToString();
        Assert.Contains("Stage=ClientConstruction", text);
        Assert.Contains("Stage=TlsNegotiation", text);
        Assert.DoesNotContain("synthetic-secret", text);
    }

    [Fact]
    public void Aggregate_DebeRecorrerTodasLasCausasSinExponerDatos()
    {
        var exception = new AggregateException("synthetic-secret",
            new FormatException("synthetic-secret"), new ArgumentException("synthetic-secret"));
        using var output = new StringWriter();
        new SafeMongoDiagnostics().WriteFailure(output, exception, MongoExecutionStage.ConfigurationLoading);
        var text = output.ToString();
        Assert.Contains("ExceptionType=System.FormatException", text);
        Assert.Contains("ExceptionType=System.ArgumentException", text);
        Assert.DoesNotContain("synthetic-secret", text);
        AssertOnlyAllowedFields(text);
    }

    [Fact]
    public void HeartbeatsRepetidos_DebenEstarDeduplicados()
    {
        var diagnostics = new SafeMongoDiagnostics();
        Parallel.For(0, 100, _ => diagnostics.ObserveTransportFailure(
            new IOException("synthetic-secret"), MongoExecutionStage.ConnectionOpening));
        using var output = new StringWriter();
        diagnostics.WriteFailure(output, new TimeoutException("synthetic-secret"),
            MongoExecutionStage.MongoCommandExecution);
        Assert.Equal(8, Lines(output.ToString()).Length);
    }

    [Fact]
    public void DisposeFallido_NoDebeOcultarErrorPrevioNiConfundirEtapas()
    {
        var diagnostics = new SafeMongoDiagnostics();
        using var output = new StringWriter();
        diagnostics.WriteFailure(output, new TimeoutException("synthetic-secret"),
            MongoExecutionStage.MongoCommandExecution);
        Assert.False(diagnostics.TryDispose(new FailingResource(), output));
        var text = output.ToString();
        Assert.Contains("Stage=MongoCommandExecution", text);
        Assert.Contains("ExceptionType=System.TimeoutException", text);
        Assert.Contains("Stage=ClientDisposal", text);
        Assert.Contains($"ExceptionType={typeof(SensitiveException).FullName}", text);
        Assert.DoesNotContain("synthetic-secret", text);
        AssertOnlyAllowedFields(text);
    }

    [Fact]
    public void DisposeSinCliente_NoDebeGenerarError()
    {
        using var output = new StringWriter();
        Assert.True(new SafeMongoDiagnostics().TryDispose(null, output));
        Assert.Empty(output.ToString());
    }

    private static void AssertOnlyAllowedFields(string text)
    {
        var lines = Lines(text);
        Assert.Equal(0, lines.Length % 4);
        for (var index = 0; index < lines.Length; index += 4)
        {
            Assert.StartsWith("Stage=", lines[index]);
            Assert.StartsWith("ExceptionType=", lines[index + 1]);
            Assert.StartsWith("InnerExceptionType=", lines[index + 2]);
            Assert.Matches("^HResult=0x[0-9A-F]{8}$", lines[index + 3]);
        }
    }

    private static string[] Lines(string value) => value.Split(
        ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private sealed class SensitiveException : Exception
    {
        public SensitiveException() => HResult = unchecked((int)0x80090304);
        public override string Message => throw new InvalidOperationException("No leer mensajes.");
        public override string ToString() => throw new InvalidOperationException("No imprimir excepciones.");
    }

    private sealed class FailingResource : IDisposable
    {
        public void Dispose() => throw new SensitiveException();
    }
}
