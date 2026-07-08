using System;
using Avalonia.Logging;

namespace Avalonia.Diagnostics.Cdp.Domains;

public class CdpLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => 
        Chrome.DevTools.Protocol.Domains.LogDomain.HasEnabledSessions;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        try
        {
            string formatted = FormatMessage(messageTemplate, propertyValues);
            Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), formatted);
        }
        catch
        {
            Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
        }
    }

    private static string FormatMessage(string template, object?[] values)
    {
        if (values == null || values.Length == 0 || string.IsNullOrEmpty(template)) return template;
        
        int valueIndex = 0;
        var sb = new System.Text.StringBuilder();
        int lastPos = 0;
        
        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] == '{')
            {
                int end = template.IndexOf('}', i);
                if (end > i)
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append(template.Substring(lastPos, i - lastPos + 1));
                        i++;
                        lastPos = i + 1;
                        continue;
                    }
                    
                    sb.Append(template.Substring(lastPos, i - lastPos));
                    
                    string placeholder = template.Substring(i + 1, end - i - 1);
                    
                    if (int.TryParse(placeholder, out int idx) && idx >= 0 && idx < values.Length)
                    {
                        sb.Append(values[idx]?.ToString() ?? "null");
                    }
                    else if (valueIndex < values.Length)
                    {
                        sb.Append(values[valueIndex]?.ToString() ?? "null");
                        valueIndex++;
                    }
                    else
                    {
                        sb.Append(template.Substring(i, end - i + 1));
                    }
                    
                    i = end;
                    lastPos = i + 1;
                }
            }
        }
        
        if (lastPos < template.Length)
        {
            sb.Append(template.Substring(lastPos));
        }
        
        return sb.ToString();
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
