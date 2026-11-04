using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Captura los mensajes de log de la API para verificar que no se filtran
/// contraseñas, tokens ni datos personales.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToArray();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal void Add(string message) => _messages.Enqueue(message);

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _provider;
        private readonly string _category;

        public CapturingLogger(CapturingLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.Add($"{_category}: {formatter(state, exception)}");

            if (exception is not null)
            {
                _provider.Add($"{_category}: exception={exception.GetType().FullName}");
            }
        }
    }
}
