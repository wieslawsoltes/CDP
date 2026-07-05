using System;
using Avalonia.Logging;

namespace Avalonia.Diagnostics.Cdp.Domains;

public class CdpLogSink : ILogSink
{
    // Gate on an enabled Log.enable session, not `true`: IsEnabled feeds Avalonia's
    // Logger.TryGet, so an always-true sink switches the whole framework to Verbose
    // logging (every property change / measure / arrange) even with no client attached.
    public bool IsEnabled(LogEventLevel level, string area) =>
        Chrome.DevTools.Protocol.Domains.LogDomain.HasEnabledSessions;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (!Chrome.DevTools.Protocol.Domains.LogDomain.HasEnabledSessions)
            return;

        Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (!Chrome.DevTools.Protocol.Domains.LogDomain.HasEnabledSessions)
            return;

        Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(
            area, level.ToString(), FormatTemplate(messageTemplate, propertyValues));
    }

    // Avalonia templates use named placeholders ({DesiredSize}), which string.Format
    // rejects — formatting them used to throw+catch a FormatException per log event.
    // Substitute placeholders positionally instead, like Avalonia's own sinks do.
    private static string FormatTemplate(string messageTemplate, object?[] propertyValues)
    {
        if (propertyValues.Length == 0 || messageTemplate.IndexOf('{') < 0)
            return messageTemplate;

        var result = new System.Text.StringBuilder(messageTemplate.Length + 32);
        var valueIndex = 0;
        for (var i = 0; i < messageTemplate.Length; i++)
        {
            var c = messageTemplate[i];
            if (c == '{')
            {
                var close = messageTemplate.IndexOf('}', i + 1);
                if (close > i && valueIndex < propertyValues.Length)
                {
                    result.Append(propertyValues[valueIndex++]);
                    i = close;
                    continue;
                }
            }

            result.Append(c);
        }

        return result.ToString();
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
