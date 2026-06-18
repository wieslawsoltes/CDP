using System;
using Avalonia.Media;

namespace CdpInspectorApp.Models;

public class LogModel
{
    public DateTime Timestamp { get; }
    public string Level { get; }
    public string Text { get; }

    public IBrush LevelBrush => Level.ToLowerInvariant() switch
    {
        "warning" => Brushes.Orange,
        "error" => Brushes.Red,
        "verbose" => Brushes.Gray,
        _ => Brushes.Green
    };

    public LogModel(DateTime ts, string level, string text)
    {
        Timestamp = ts;
        Level = level;
        Text = text;
    }
}
