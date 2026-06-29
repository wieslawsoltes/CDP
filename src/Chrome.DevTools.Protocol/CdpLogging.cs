using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chrome.DevTools.Protocol;

public static class CdpLogging
{
    private static ILoggerFactory? _loggerFactory;

    public static ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set => _loggerFactory = value;
    }

    public static ILogger CreateLogger(string categoryName)
    {
        return new CdpLogger(categoryName);
    }

    public static ILogger<T> CreateLogger<T>()
    {
        return CdpLogger<T>.Instance;
    }
}

public sealed class CdpLogger : ILogger
{
    private readonly string _categoryName;

    public CdpLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    private ILogger Logger => CdpLogging.LoggerFactory?.CreateLogger(_categoryName) ?? NullLogger.Instance;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logger.Log(logLevel, eventId, state, exception, formatter);
    }
}

public sealed class CdpLogger<T> : ILogger<T>
{
    public static readonly CdpLogger<T> Instance = new();

    private CdpLogger() { }

    private ILogger Logger => CdpLogging.LoggerFactory?.CreateLogger<T>() ?? NullLogger<T>.Instance;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
