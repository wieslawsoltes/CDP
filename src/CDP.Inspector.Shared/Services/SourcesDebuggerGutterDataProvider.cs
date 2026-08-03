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
    }

    public event EventHandler? VisualInvalidated;

    public void RenderIndicator(DrawingContext drawingContext, int lineNumber, double yCenter, double width, double height, bool isHovered)
    {
        var hasBreakpoint = _viewModel.V8Breakpoints.Any(bp => IsCurrentSource(bp) &&
            (bp.DisplayLineNumber ?? bp.LineNumber) + 1 == lineNumber);
        var isActive = _viewModel.ActiveDebugLine == lineNumber;
        var center = new Point(width / 2, yCenter);

        if (isActive)
        {
            var arrow = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(center.X - 5, yCenter - 6), IsClosed = true };
            figure.Segments.Add(new LineSegment { Point = new Point(center.X - 5, yCenter + 6) });
            figure.Segments.Add(new LineSegment { Point = new Point(center.X + 6, yCenter) });
            arrow.Figures.Add(figure);
            drawingContext.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 202, 40)), null, arrow);
            if (!hasBreakpoint) return;
        }

        if (hasBreakpoint)
        {
            drawingContext.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(229, 57, 53)),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 138, 128)), 1),
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

    private void BreakpointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        VisualInvalidated?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel.V8Breakpoints.CollectionChanged -= BreakpointsOnCollectionChanged;
    }
}
