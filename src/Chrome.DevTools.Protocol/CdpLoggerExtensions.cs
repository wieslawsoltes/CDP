using System;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol;

public static partial class CdpLoggerExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "CDP Server started on port {Port}")]
    public static partial void ServerStarted(this ILogger logger, int port);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "CDP Server stopped")]
    public static partial void ServerStopped(this ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "CDP Server error: {Error}")]
    public static partial void ServerError(this ILogger logger, string error, Exception? exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Client connected to target {TargetId} (Host: {Host})")]
    public static partial void ClientConnected(this ILogger logger, string targetId, string host);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Client disconnected")]
    public static partial void ClientDisconnected(this ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Client connection failed: {Error}")]
    public static partial void ClientConnectionFailed(this ILogger logger, string error, Exception? exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Sending CDP command: {Method} (ID: {Id})")]
    public static partial void SendingCommand(this ILogger logger, string method, int id);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Received CDP response: {Id}")]
    public static partial void ReceivedResponse(this ILogger logger, int id);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Received CDP event: {Method}")]
    public static partial void ReceivedEvent(this ILogger logger, string method);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "{Component}: {Message}")]
    public static partial void LogWarningMessage(this ILogger logger, string component, string message, Exception? exception = null);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "{Component}: {Message}")]
    public static partial void LogErrorMessage(this ILogger logger, string component, string message, Exception? exception = null);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "{Component}: {Message}")]
    public static partial void LogInfoMessage(this ILogger logger, string component, string message);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "[{SessionType}] Incoming: method={Method}, id={Id}, params={Params}")]
    public static partial void LogIncomingMessage(this ILogger logger, string sessionType, string method, int id, string @params);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "[{SessionType}] Outgoing: {Payload}")]
    public static partial void LogOutgoingMessage(this ILogger logger, string sessionType, string payload);

    [LoggerMessage(EventId = 15, Level = LogLevel.Debug, Message = "Screencast: {Message}")]
    public static partial void LogScreencastDebug(this ILogger logger, string message);

    [LoggerMessage(EventId = 16, Level = LogLevel.Error, Message = "Screencast error: {Message}")]
    public static partial void LogScreencastError(this ILogger logger, string message, Exception? exception);

    [LoggerMessage(EventId = 17, Level = LogLevel.Debug, Message = "[Playwright Debug] {Message}")]
    public static partial void LogPlaywrightDebug(this ILogger logger, string message);

    [LoggerMessage(EventId = 18, Level = LogLevel.Error, Message = "[Jint Error] {Message}")]
    public static partial void LogJintError(this ILogger logger, string message, Exception? exception);

    [LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "[Profiler] {EngineName}: {Message}")]
    public static partial void LogProfilerInfo(this ILogger logger, string engineName, string message);

    [LoggerMessage(EventId = 20, Level = LogLevel.Error, Message = "[Profiler] {EngineName} error: {Message}")]
    public static partial void LogProfilerError(this ILogger logger, string engineName, string message, Exception? exception);

    [LoggerMessage(EventId = 21, Level = LogLevel.Debug, Message = "[Network Trace] {Message}")]
    public static partial void LogNetworkTrace(this ILogger logger, string message);

    [LoggerMessage(EventId = 22, Level = LogLevel.Debug, Message = "[Fetch Debug] {Message}")]
    public static partial void LogFetchDebug(this ILogger logger, string message);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "[Server Debug] {Message}")]
    public static partial void LogServerDebug(this ILogger logger, string message);

    [LoggerMessage(EventId = 24, Level = LogLevel.Debug, Message = "[Memory Diagnostic] {Message}")]
    public static partial void LogMemoryDiagnostic(this ILogger logger, string message);

    [LoggerMessage(EventId = 25, Level = LogLevel.Warning, Message = "[Memory Leak] {Message}")]
    public static partial void LogMemoryLeak(this ILogger logger, string message);

    [LoggerMessage(EventId = 26, Level = LogLevel.Debug, Message = "[ARIA Debug] {Message}")]
    public static partial void LogAriaDebug(this ILogger logger, string message);
}
