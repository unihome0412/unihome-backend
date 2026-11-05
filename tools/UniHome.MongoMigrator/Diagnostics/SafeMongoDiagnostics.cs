using System.Globalization;
using System.Security.Authentication;
using MongoDB.Driver;

namespace UniHome.MongoMigrator.Diagnostics;

/// <summary>
/// Conserva exclusivamente metadatos permitidos. No retiene las excepciones,
/// mensajes, Data, stack traces, direcciones de servidores ni configuración.
/// Los eventos de transporte pueden contener la causa que un timeout SDAM no
/// conserva en InnerException. No se analizan mensajes para intentar recuperarla.
/// </summary>
public sealed class SafeMongoDiagnostics
{
    private const int MaximumEntries = 32;
    private readonly object _gate = new();
    private readonly HashSet<SafeMongoFailure> _transportFailures = [];

    public bool TryDispose(IDisposable? resource, TextWriter writer)
    {
        try
        {
            resource?.Dispose();
            return true;
        }
        catch (Exception exception)
        {
            WriteFailure(writer, exception, MongoExecutionStage.ClientDisposal);
            return false;
        }
    }

    public void ObserveTransportFailure(Exception exception, MongoExecutionStage stage)
    {
        var chain = EnumerateExceptions(exception).ToArray();
        var actualStage = chain.Any(item => item is AuthenticationException)
            ? MongoExecutionStage.TlsNegotiation
            : chain.Any(item => item is MongoAuthenticationException)
                ? MongoExecutionStage.MongoAuthentication
                : stage;

        lock (_gate)
        {
            foreach (var item in chain)
            {
                if (_transportFailures.Count >= MaximumEntries)
                {
                    break;
                }
                _transportFailures.Add(Capture(item, actualStage));
            }
        }
    }

    public void WriteFailure(TextWriter writer, Exception exception, MongoExecutionStage stage)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(exception);

        if (stage is MongoExecutionStage.ClientConstruction or MongoExecutionStage.MongoCommandExecution or
            MongoExecutionStage.ConnectionOpening or MongoExecutionStage.ServerSelection)
        {
            // También cubre cadenas TLS directas, no solo fallos capturados por SDAM.
            ObserveTransportFailure(exception, stage);
        }

        SafeMongoFailure[] observed;
        lock (_gate)
        {
            observed = _transportFailures.ToArray();
        }

        var failures = EnumerateExceptions(exception).Select(item => Capture(item, stage))
            .Concat(observed).Distinct();
        foreach (var failure in failures)
        {
            writer.WriteLine($"Stage={failure.Stage}");
            writer.WriteLine($"ExceptionType={failure.ExceptionType}");
            writer.WriteLine($"InnerExceptionType={failure.InnerExceptionType ?? "(none)"}");
            writer.WriteLine($"HResult=0x{unchecked((uint)failure.HResult).ToString("X8", CultureInfo.InvariantCulture)}");
        }
    }

    private static SafeMongoFailure Capture(Exception exception, MongoExecutionStage stage) => new(
        stage,
        exception.GetType().FullName ?? exception.GetType().Name,
        exception.InnerException?.GetType().FullName,
        exception.HResult);

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        var pending = new Queue<Exception>();
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Enqueue(exception);
        while (pending.Count > 0 && seen.Count < MaximumEntries)
        {
            var current = pending.Dequeue();
            if (!seen.Add(current))
            {
                continue;
            }
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.Take(MaximumEntries))
                {
                    pending.Enqueue(inner);
                }
            }
            else if (current.InnerException is { } inner)
            {
                pending.Enqueue(inner);
            }
        }
    }

    private sealed record SafeMongoFailure(
        MongoExecutionStage Stage, string ExceptionType, string? InnerExceptionType, int HResult);
}
