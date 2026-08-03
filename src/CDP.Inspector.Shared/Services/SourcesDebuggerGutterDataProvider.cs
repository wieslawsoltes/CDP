using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using XamlPlayground.Editor.Minimap.Inline;

namespace CdpInspectorApp.Services;

public sealed class SourcesDebuggerGutterDataProvider : IGutterMarginDataProvider, IDisposable
{
    private readonly SourcesViewModel _viewModel;

    public SourcesDebuggerGutterDataProvider(SourcesViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.V8Breakpoints.CollectionChanged += BreakpointsOnCollectionChanged;
        foreach (var breakpoint in _viewModel.V8Breakpoints) breakpoint.PropertyChanged += BreakpointOnPropertyChanged;
    }

    public event EventHandler? VisualInvalidated;

    public void RenderIndicator(DrawingContext drawingContext, int lineNumber, double yCenter, double width, double height, bool isHovered)
    {
        var breakpoint = _viewModel.V8Breakpoints.FirstOrDefault(bp => IsCurrentSource(bp) &&
            (bp.DisplayLineNumber ?? bp.LineNumber) + 1 == lineNumber);
        var hasBreakpoint = breakpoint is not null;
        var isActive = _viewModel.ActiveDebugLine == lineNumber;
        var center = new Point(width / 2, yCenter);

        if (isActive)
        {
            var arrow = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(center.X - 5, yCenter - 6), IsClosed = true };
            figure.Segments!.Add(new LineSegment { Point = new Point(center.X - 5, yCenter + 6) });
            figure.Segments.Add(new LineSegment { Point = new Point(center.X + 6, yCenter) });
            arrow.Figures!.Add(figure);
            drawingContext.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 202, 40)), null, arrow);
            if (!hasBreakpoint) return;
        }

        if (hasBreakpoint)
        {
            var fill = breakpoint!.IsEnabled
                ? breakpoint.Kind switch
                {
                    V8BreakpointKinds.Logpoint => Color.FromRgb(171, 71, 188),
                    V8BreakpointKinds.Conditional => Color.FromRgb(251, 140, 0),
                    _ => Color.FromRgb(229, 57, 53)
                }
                : Color.FromRgb(95, 99, 104);
            var stroke = breakpoint.IsResolved ? Color.FromRgb(255, 205, 210) : Color.FromRgb(189, 189, 189);
            drawingContext.DrawEllipse(
                new SolidColorBrush(fill),
                new Pen(new SolidColorBrush(stroke), breakpoint.IsResolved ? 1 : 2),
                center, 5, 5);
        }
        else if (isHovered)
        {
            drawingContext.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(90, 229, 57, 53)),
                null,
                center, 4, 4);
        }
    }

    public void OnLineClicked(int lineNumber)
    {
        if (_viewModel.ToggleBreakpointCommand.CanExecute(lineNumber))
        {
            _viewModel.ToggleBreakpointCommand.Execute(lineNumber);
        }
    }

    private bool IsCurrentSource(V8BreakpointModel breakpoint)
    {
        if (_viewModel.SelectedRuntimeScript is { } script)
        {
            return (!string.IsNullOrWhiteSpace(breakpoint.ScriptId) && breakpoint.ScriptId == script.ScriptId) ||
                   (!string.IsNullOrWhiteSpace(breakpoint.Url) && breakpoint.Url == script.Url);
        }

        var path = _viewModel.SelectedFile?.Path;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(breakpoint.Url)) return false;
        return breakpoint.Url.Replace('\\', '/').EndsWith(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SourcesViewModel.ActiveDebugLine) or
            nameof(SourcesViewModel.SelectedRuntimeScript) or nameof(SourcesViewModel.SelectedFile))
        {
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void BreakpointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (V8BreakpointModel breakpoint in e.OldItems) breakpoint.PropertyChanged -= BreakpointOnPropertyChanged;
        }
        if (e.NewItems is not null)
        {
            foreach (V8BreakpointModel breakpoint in e.NewItems) breakpoint.PropertyChanged += BreakpointOnPropertyChanged;
        }
        VisualInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void BreakpointOnPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        VisualInvalidated?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel.V8Breakpoints.CollectionChanged -= BreakpointsOnCollectionChanged;
        foreach (var breakpoint in _viewModel.V8Breakpoints) breakpoint.PropertyChanged -= BreakpointOnPropertyChanged;
    }
}
