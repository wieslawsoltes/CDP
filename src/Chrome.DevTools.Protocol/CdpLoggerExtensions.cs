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
}
