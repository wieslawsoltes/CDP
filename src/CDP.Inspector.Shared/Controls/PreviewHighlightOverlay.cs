using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Controls;

public class PreviewHighlightOverlay : Control
{
    public static readonly StyledProperty<SimulationViewModel?> SimulationProperty =
        AvaloniaProperty.Register<PreviewHighlightOverlay, SimulationViewModel?>(nameof(Simulation));

    public SimulationViewModel? Simulation
    {
        get => GetValue(SimulationProperty);
        set => SetValue(SimulationProperty, value);
    }

    static PreviewHighlightOverlay()
    {
        SimulationProperty.Changed.AddClassHandler<PreviewHighlightOverlay>((x, e) => x.OnSimulationChanged(e));
    }

    private void OnSimulationChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is SimulationViewModel oldSim)
        {
            oldSim.PropertyChanged -= OnSimulationPropertyChanged;
        }
        if (e.NewValue is SimulationViewModel newSim)
        {
            newSim.PropertyChanged += OnSimulationPropertyChanged;
        }
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Simulation != null)
        {
            Simulation.PropertyChanged -= OnSimulationPropertyChanged;
            Simulation.PropertyChanged += OnSimulationPropertyChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Simulation != null)
        {
            Simulation.PropertyChanged -= OnSimulationPropertyChanged;
        }
    }

    private void OnSimulationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimulationViewModel.HighlightBoxModel) ||
            e.PropertyName == nameof(SimulationViewModel.IsHighlightOverlayVisible) ||
            e.PropertyName == nameof(SimulationViewModel.DeviceWidth) ||
            e.PropertyName == nameof(SimulationViewModel.DeviceHeight))
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var sim = Simulation;
        if (sim == null || !sim.IsHighlightOverlayVisible || sim.HighlightBoxModel == null) return;

        double deviceWidth = sim.DeviceWidth;
        double deviceHeight = sim.DeviceHeight;
        if (deviceWidth <= 0 || deviceHeight <= 0) return;

        double localWidth = Bounds.Width;
        double localHeight = Bounds.Height;
        if (localWidth <= 0 || localHeight <= 0) return;

        double scaleX = localWidth / deviceWidth;
        double scaleY = localHeight / deviceHeight;

        var model = sim.HighlightBoxModel;
        var contentQuad = model["content"] as JsonArray;
        var paddingQuad = model["padding"] as JsonArray;
        var borderQuad = model["border"] as JsonArray;
        var marginQuad = model["margin"] as JsonArray;

        Rect? GetLocalRect(JsonArray? quad)
        {
            if (quad == null || quad.Count < 8) return null;
            
            double x1 = quad[0]!.GetValue<double>();
            double y1 = quad[1]!.GetValue<double>();
            double x2 = quad[2]!.GetValue<double>();
            double y2 = quad[3]!.GetValue<double>();
            double x3 = quad[4]!.GetValue<double>();
            double y3 = quad[5]!.GetValue<double>();
            double x4 = quad[6]!.GetValue<double>();
            double y4 = quad[7]!.GetValue<double>();

            double minX = Math.Min(Math.Min(x1, x2), Math.Min(x3, x4));
            double maxX = Math.Max(Math.Max(x1, x2), Math.Max(x3, x4));
            double minY = Math.Min(Math.Min(y1, y2), Math.Min(y3, y4));
            double maxY = Math.Max(Math.Max(y1, y2), Math.Max(y3, y4));

            return new Rect(minX * scaleX, minY * scaleY, (maxX - minX) * scaleX, (maxY - minY) * scaleY);
        }

        var contentRect = GetLocalRect(contentQuad);
        var paddingRect = GetLocalRect(paddingQuad);
        var borderRect = GetLocalRect(borderQuad);
        var marginRect = GetLocalRect(marginQuad);

        // 1. Draw Margin Rect (Orange)
        if (marginRect.HasValue)
        {
            var marginBrush = new SolidColorBrush(Color.FromArgb(32, 246, 178, 107));
            context.DrawRectangle(marginBrush, null, marginRect.Value);
        }

        // 2. Draw Border Rect (Yellow-green)
        if (borderRect.HasValue)
        {
            var borderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 229, 153));
            context.DrawRectangle(borderBrush, null, borderRect.Value);
        }

        // 3. Draw Padding Rect (Green)
        if (paddingRect.HasValue)
        {
            var paddingBrush = new SolidColorBrush(Color.FromArgb(32, 147, 196, 125));
            context.DrawRectangle(paddingBrush, null, paddingRect.Value);
        }

        // 4. Draw Content Rect (Blue with solid border line)
        if (contentRect.HasValue)
        {
            var contentBrush = new SolidColorBrush(Color.FromArgb(64, 120, 170, 240));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 120, 170, 240)), 1.5);
            context.DrawRectangle(contentBrush, borderPen, contentRect.Value);
        }

        // 5. Draw Tooltip
        if (borderRect.HasValue)
        {
            double w = model["width"]?.GetValue<double>() ?? (borderRect.Value.Width / scaleX);
            double h = model["height"]?.GetValue<double>() ?? (borderRect.Value.Height / scaleY);

            string label = $"{sim.HighlightElementType} | {w:0}x{h:0}";
            if (!string.IsNullOrEmpty(sim.HighlightAxRole) && sim.HighlightAxRole != "None" && sim.HighlightAxRole != "Custom")
            {
                label += $" | Role: {sim.HighlightAxRole.ToLowerInvariant()}";
                if (!string.IsNullOrEmpty(sim.HighlightAxName) && sim.HighlightAxName != "None")
                {
                    label += $" Name: \"{sim.HighlightAxName}\"";
                }
            }
            else if (!string.IsNullOrEmpty(sim.HighlightAxName) && sim.HighlightAxName != "None")
            {
                label += $" | Name: \"{sim.HighlightAxName}\"";
            }

            var text = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                Brushes.White
            );

            double x = borderRect.Value.X;
            double y = borderRect.Value.Y;
            double tooltipX = x;
            double tooltipY = y - text.Height - 6;

            if (tooltipY < 0)
            {
                tooltipY = y + borderRect.Value.Height + 6;
            }

            if (tooltipX + text.Width + 8 > localWidth)
            {
                tooltipX = Math.Max(0, localWidth - text.Width - 8);
            }

            var textRect = new Rect(tooltipX, tooltipY - 2, text.Width + 8, text.Height + 4);
            var bgBrush = new SolidColorBrush(Color.FromArgb(220, 33, 33, 33));

            context.DrawRectangle(bgBrush, null, textRect);
            context.DrawText(text, new Point(tooltipX + 4, tooltipY));
        }
    }
}
