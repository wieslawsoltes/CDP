using System;
using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CdpInspectorApp.Models;
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

    private readonly DispatcherTimer _animationTimer;
    private double _animationProgress = 0.0;

    public PreviewHighlightOverlay()
    {
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _animationTimer.Tick += (s, e) =>
        {
            _animationProgress += 0.05;
            if (_animationProgress > 1.0)
            {
                _animationProgress = 0.0;
            }
            InvalidateVisual();
        };
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
        else if (e.PropertyName == nameof(SimulationViewModel.ActiveReplayIndicator))
        {
            var sim = Simulation;
            if (sim?.ActiveReplayIndicator != null && 
                (sim.ActiveReplayIndicator.Action == "tapOn" || 
                 sim.ActiveReplayIndicator.Action == "doubleTapOn" || 
                 sim.ActiveReplayIndicator.Action == "longPressOn"))
            {
                _animationProgress = 0.0;
                if (!_animationTimer.IsEnabled)
                {
                    _animationTimer.Start();
                }
            }
            else
            {
                if (_animationTimer.IsEnabled)
                {
                    _animationTimer.Stop();
                }
            }
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var sim = Simulation;
        if (sim == null) return;

        double deviceWidth = sim.DeviceWidth;
        double deviceHeight = sim.DeviceHeight;
        if (deviceWidth <= 0 || deviceHeight <= 0) return;

        double localWidth = Bounds.Width;
        double localHeight = Bounds.Height;
        if (localWidth <= 0 || localHeight <= 0) return;

        double scaleX = localWidth / deviceWidth;
        double scaleY = localHeight / deviceHeight;

        // 1. Hover/Inspector Highlights
        if (sim.IsHighlightOverlayVisible && sim.HighlightBoxModel != null)
        {
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

            // Draw Margin Rect (Orange)
            if (marginRect.HasValue)
            {
                var marginBrush = new SolidColorBrush(Color.FromArgb(32, 246, 178, 107));
                context.DrawRectangle(marginBrush, null, marginRect.Value);
            }

            // Draw Border Rect (Yellow-green)
            if (borderRect.HasValue)
            {
                var borderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 229, 153));
                context.DrawRectangle(borderBrush, null, borderRect.Value);
            }

            // Draw Padding Rect (Green)
            if (paddingRect.HasValue)
            {
                var paddingBrush = new SolidColorBrush(Color.FromArgb(32, 147, 196, 125));
                context.DrawRectangle(paddingBrush, null, paddingRect.Value);
            }

            // Draw Content Rect (Blue with solid border line)
            if (contentRect.HasValue)
            {
                var contentBrush = new SolidColorBrush(Color.FromArgb(64, 120, 170, 240));
                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 120, 170, 240)), 1.5);
                context.DrawRectangle(contentBrush, borderPen, contentRect.Value);
            }

            // Draw Tooltip
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

        // 2. Active Replay Indicators
        var indicator = sim.ActiveReplayIndicator;
        if (indicator != null)
        {
            Point MapPoint(double rawX, double rawY)
            {
                return new Point(rawX * scaleX, rawY * scaleY);
            }

            // Draw Concentric Touch Ripple
            if (indicator.X.HasValue && indicator.Y.HasValue && 
                (indicator.Action == "tapOn" || indicator.Action == "doubleTapOn" || indicator.Action == "longPressOn"))
            {
                var p = MapPoint(indicator.X.Value, indicator.Y.Value);
                
                // Filled inner circle
                var innerBrush = new SolidColorBrush(Color.FromArgb(140, 186, 104, 200)); // Maestro purple
                context.DrawGeometry(innerBrush, null, new EllipseGeometry(new Rect(p.X - 8, p.Y - 8, 16, 16)));
                
                // Translucent outer pulsing ring
                double maxPulseRadius = 24.0;
                double pulseRadius = 8.0 + (maxPulseRadius - 8.0) * _animationProgress;
                byte ringAlpha = (byte)(255 * (1.0 - _animationProgress));
                var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(ringAlpha, 186, 104, 200)), 2.0);
                context.DrawGeometry(null, ringPen, new EllipseGeometry(new Rect(p.X - pulseRadius, p.Y - pulseRadius, pulseRadius * 2, pulseRadius * 2)));
            }

            // Draw Swipe / Drag-and-drop Arrow Path
            if (indicator.X.HasValue && indicator.Y.HasValue && indicator.EndX.HasValue && indicator.EndY.HasValue)
            {
                var pStart = MapPoint(indicator.X.Value, indicator.Y.Value);
                var pEnd = MapPoint(indicator.EndX.Value, indicator.EndY.Value);

                var trackBrush = new SolidColorBrush(Color.FromArgb(120, 186, 104, 200));
                var trackPen = new Pen(trackBrush, 3.5, lineCap: PenLineCap.Round);
                
                context.DrawLine(trackPen, pStart, pEnd);
                context.DrawGeometry(new SolidColorBrush(Color.FromArgb(200, 186, 104, 200)), null, new EllipseGeometry(new Rect(pStart.X - 5, pStart.Y - 5, 10, 10)));

                double dx = pEnd.X - pStart.X;
                double dy = pEnd.Y - pStart.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0)
                {
                    dx /= len;
                    dy /= len;
                    double arrowLength = 12.0;
                    double angle = Math.PI / 6.0;
                    
                    var arrowPoint1 = new Point(
                        pEnd.X - arrowLength * (dx * Math.Cos(angle) - dy * Math.Sin(angle)),
                        pEnd.Y - arrowLength * (dy * Math.Cos(angle) + dx * Math.Sin(angle))
                    );
                    var arrowPoint2 = new Point(
                        pEnd.X - arrowLength * (dx * Math.Cos(angle) + dy * Math.Sin(angle)),
                        pEnd.Y - arrowLength * (dy * Math.Cos(angle) - dx * Math.Sin(angle))
                    );
                    
                    context.DrawLine(trackPen, pEnd, arrowPoint1);
                    context.DrawLine(trackPen, pEnd, arrowPoint2);
                }
            }

            // Draw Scroll Indicator
            if (indicator.Action == "scroll" && indicator.X.HasValue && indicator.Y.HasValue)
            {
                var p = MapPoint(indicator.X.Value, indicator.Y.Value);
                var badgeBrush = new SolidColorBrush(Color.FromArgb(160, 30, 144, 255));
                context.DrawGeometry(badgeBrush, null, new EllipseGeometry(new Rect(p.X - 12, p.Y - 12, 24, 24)));
                
                var text = new FormattedText(
                    "↕",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.White
                );
                context.DrawText(text, new Point(p.X - text.Width / 2.0, p.Y - text.Height / 2.0));
            }

            // Draw Assertion Bounding Box & Status Badge
            if (indicator.BoxModel != null)
            {
                var contentQuad = indicator.BoxModel["content"] as JsonArray;
                var borderQuad = indicator.BoxModel["border"] as JsonArray ?? contentQuad;
                
                Rect? GetIndicatorLocalRect(JsonArray? quad)
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

                var rect = GetIndicatorLocalRect(borderQuad);
                if (rect.HasValue)
                {
                    Color fillColor;
                    Color borderColor;
                    string statusChar;
                    
                    if (indicator.Status == ReplayIndicatorStatus.Passed)
                    {
                        fillColor = Color.FromArgb(48, 15, 157, 88);
                        borderColor = Color.FromRgb(15, 157, 88);
                        statusChar = "✔";
                    }
                    else if (indicator.Status == ReplayIndicatorStatus.Failed)
                    {
                        fillColor = Color.FromArgb(48, 197, 34, 31);
                        borderColor = Color.FromRgb(197, 34, 31);
                        statusChar = "✘";
                    }
                    else
                    {
                        fillColor = Color.FromArgb(32, 26, 115, 232);
                        borderColor = Color.FromRgb(26, 115, 232);
                        statusChar = "●";
                    }

                    var fillBrush = new SolidColorBrush(fillColor);
                    var borderPen = new Pen(new SolidColorBrush(borderColor), 2.5);
                    
                    context.DrawRectangle(fillBrush, borderPen, rect.Value);

                    double badgeRadius = 9.0;
                    double badgeX = rect.Value.Right;
                    double badgeY = rect.Value.Top;
                    
                    context.DrawGeometry(new SolidColorBrush(borderColor), null, new EllipseGeometry(new Rect(badgeX - badgeRadius, badgeY - badgeRadius, badgeRadius * 2, badgeRadius * 2)));
                    
                    var text = new FormattedText(
                        statusChar,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        11,
                        Brushes.White
                    );
                    context.DrawText(text, new Point(badgeX - text.Width / 2.0, badgeY - text.Height / 2.0));
                }
            }

            // Draw Text Input Feedback bubble
            if (indicator.Action == "inputText" && !string.IsNullOrEmpty(indicator.Value) && indicator.X.HasValue && indicator.Y.HasValue)
            {
                var p = MapPoint(indicator.X.Value, indicator.Y.Value);
                string feedbackText = $"Typing: \"{indicator.Value}\"";
                
                var text = new FormattedText(
                    feedbackText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.White
                );
                
                double tooltipX = Math.Max(4, p.X - text.Width / 2.0 - 4);
                double tooltipY = p.Y - text.Height - 16;
                if (tooltipY < 4) tooltipY = p.Y + 16;

                var tooltipRect = new Rect(tooltipX, tooltipY, text.Width + 8, text.Height + 4);
                var tooltipBg = new SolidColorBrush(Color.FromArgb(220, 26, 115, 232));
                
                context.DrawRectangle(tooltipBg, null, tooltipRect);
                context.DrawText(text, new Point(tooltipX + 4, tooltipY + 2));
            }

            // Draw bottom status HUD Overlay
            double hudHeight = 30.0;
            double hudY = localHeight - hudHeight;
            var hudRect = new Rect(0, hudY, localWidth, hudHeight);
            var hudBg = new SolidColorBrush(Color.FromArgb(225, 28, 29, 32));
            
            context.DrawRectangle(hudBg, null, hudRect);
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(60, 64, 67)), 1.0), new Point(0, hudY), new Point(localWidth, hudY));

            string actionDisplay = indicator.Action switch
            {
                "launchApp" => "Launch App",
                "tapOn" => "Tap On",
                "doubleTapOn" => "Double Tap On",
                "longPressOn" => "Long Press On",
                "inputText" => "Input Text",
                "clearText" => "Clear Text",
                "assertVisible" => "Assert Visible",
                "assertNotVisible" => "Assert Not Visible",
                "delay" => "Delay",
                "back" => "Go Back",
                "scroll" => "Scroll",
                "pressKey" => "Press Key",
                "dragAndDrop" => "Drag And Drop",
                "swipe" => "Swipe",
                _ => indicator.Action
            };
            
            string hudMsg = $"Replaying: {actionDisplay}";
            if (!string.IsNullOrEmpty(indicator.Selector))
            {
                hudMsg += $" '{indicator.Selector}'";
            }
            if (!string.IsNullOrEmpty(indicator.Value))
            {
                hudMsg += $" with value \"{indicator.Value}\"";
            }

            Color statusColor;
            string statusText;
            if (indicator.Status == ReplayIndicatorStatus.Passed)
            {
                statusColor = Color.FromRgb(15, 157, 88);
                statusText = "PASSED";
            }
            else if (indicator.Status == ReplayIndicatorStatus.Failed)
            {
                statusColor = Color.FromRgb(197, 34, 31);
                statusText = $"FAILED: {indicator.ErrorMessage}";
            }
            else
            {
                statusColor = Color.FromRgb(26, 115, 232);
                statusText = "RUNNING";
            }

            var statusFmt = new FormattedText(
                statusText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                new SolidColorBrush(statusColor)
            );
            context.DrawText(statusFmt, new Point(10, hudY + (hudHeight - statusFmt.Height) / 2.0));

            var msgFmt = new FormattedText(
                hudMsg,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                Brushes.White
            );
            context.DrawText(msgFmt, new Point(20 + statusFmt.Width, hudY + (hudHeight - msgFmt.Height) / 2.0));
        }
    }
}
