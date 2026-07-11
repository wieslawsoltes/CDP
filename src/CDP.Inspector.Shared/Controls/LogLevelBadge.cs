using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class LogLevelBadge : Control
{
    public static readonly StyledProperty<string?> LevelProperty =
        AvaloniaProperty.Register<LogLevelBadge, string?>(nameof(Level), "INFO");

    public string? Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    static LogLevelBadge()
    {
        AffectsRender<LogLevelBadge>(LevelProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        string levelStr = Level?.ToUpperInvariant() ?? "INFO";

        // Determine colors based on log level
        Color levelColor;
        switch (levelStr)
        {
            case "ERROR":
            case "ERR":
            case "CRITICAL":
                levelColor = Color.Parse("#f28b82"); // Soft Red
                break;
            case "WARNING":
            case "WARN":
                levelColor = Color.Parse("#fdd663"); // Soft Yellow/Orange
                break;
            case "VERBOSE":
            case "DEBUG":
            case "TRACE":
                levelColor = Color.Parse("#9aa0a6"); // Soft Gray
                break;
            case "INFO":
            default:
                levelColor = Color.Parse("#81c995"); // Soft Green
                break;
        }

        // Draw capsule background: 12% opacity color fill, 25% opacity border
        var bgBrush = new SolidColorBrush(Color.FromArgb((byte)(levelColor.A * 0.12), levelColor.R, levelColor.G, levelColor.B));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(levelColor.A * 0.25), levelColor.R, levelColor.G, levelColor.B)), 1.0);
        context.DrawRectangle(bgBrush, borderPen, new Rect(0, 0, w, h), h / 2.0, h / 2.0);

        // Draw level label text
        var textBrush = new SolidColorBrush(levelColor);
        var formattedText = new FormattedText(
            levelStr,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            10.0,
            textBrush
        );

        var textPoint = new Point(
            w / 2.0 - formattedText.Width / 2.0,
            h / 2.0 - formattedText.Height / 2.0
        );
        context.DrawText(formattedText, textPoint);
    }
}
