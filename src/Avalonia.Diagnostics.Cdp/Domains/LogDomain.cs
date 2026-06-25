using System;
using Avalonia.Logging;

namespace Avalonia.Diagnostics.Cdp.Domains;

public class CdpLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => true;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        try
        {
            string formatted = string.Format(messageTemplate, propertyValues);
            Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), formatted);
        }
        catch
        {
            Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
        }
    }
}

public class CompositeLogSink : ILogSink
{
    public ILogSink? OriginalSink { get; }
    private readonly ILogSink _newSink;

    public CompositeLogSink(ILogSink? original, ILogSink newSink)
    {
        OriginalSink = original;
        _newSink = newSink;
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return (OriginalSink?.IsEnabled(level, area) ?? false) || _newSink.IsEnabled(level, area);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        OriginalSink?.Log(level, area, source, messageTemplate);
        _newSink.Log(level, area, source, messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        OriginalSink?.Log(level, area, source, messageTemplate, propertyValues);
        _newSink.Log(level, area, source, messageTemplate, propertyValues);
    }
}
