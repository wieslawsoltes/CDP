using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

namespace XamlPlayground.Editor.Minimap.Inline;

public class ReplayGutterMargin : AbstractMargin
{
    private readonly TestStudioViewModel _viewModel;
    private readonly RecorderViewModel _recorderViewModel;
    private int _hoveredLineNumber = -1;

    public ReplayGutterMargin(TestStudioViewModel viewModel, RecorderViewModel recorderViewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _recorderViewModel = recorderViewModel ?? throw new ArgumentNullException(nameof(recorderViewModel));

        // Redraw on state and list changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _recorderViewModel.PropertyChanged += RecorderViewModel_PropertyChanged;
        _viewModel.Steps.CollectionChanged += Steps_CollectionChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioViewModel.IsExecuting) ||
            e.PropertyName == nameof(TestStudioViewModel.YamlCode) ||
            e.PropertyName == nameof(TestStudioViewModel.Steps))
        {
            Dispatcher.UIThread.Post(InvalidateVisual);
        }
    }

    private void RecorderViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecorderViewModel.IsRecording))
        {
            Dispatcher.UIThread.Post(InvalidateVisual);
        }
    }

    private void Steps_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.VisualLinesChanged -= TextView_VisualLinesChanged;
            oldTextView.ScrollOffsetChanged -= TextView_ScrollOffsetChanged;
        }
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += TextView_VisualLinesChanged;
            newTextView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
        }
    }

    private void TextView_VisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();
    private void TextView_ScrollOffsetChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(24, 0); // 24 pixels wide to hold indicators cleanly
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (TextView == null || !TextView.VisualLinesValid) return;

        var pos = e.GetPosition(TextView);
        var visualLine = TextView.GetVisualLineFromVisualTop(pos.Y + TextView.VerticalOffset);
        int newLine = visualLine != null ? visualLine.FirstDocumentLine.LineNumber : -1;
        if (newLine != _hoveredLineNumber)
        {
            _hoveredLineNumber = newLine;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredLineNumber != -1)
        {
            _hoveredLineNumber = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (TextView == null || !TextView.VisualLinesValid || _viewModel.IsExecuting || _recorderViewModel.IsRecording) return;

        var pos = e.GetPosition(TextView);
        var visualLine = TextView.GetVisualLineFromVisualTop(pos.Y + TextView.VerticalOffset);
        if (visualLine == null) return;
        int lineNumber = visualLine.FirstDocumentLine.LineNumber;

        // Find matching step starting on this line
        var step = GetAllSteps(_viewModel.Steps).FirstOrDefault(s => s.StartLine == lineNumber);
        if (step != null)
        {
            e.Handled = true;
            // Execute this step asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await _viewModel.RunSingleStepAsync(step);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayGutterMargin] Single-step execution failed: {ex.Message}");
                }
            });
        }
    }

    private IEnumerable<TestStudioStepModel> GetAllSteps(IEnumerable<TestStudioStepModel> steps)
    {
        foreach (var step in steps)
        {
            yield return step;
            if (step.NestedSteps != null)
            {
                foreach (var nested in GetAllSteps(step.NestedSteps))
                {
                    yield return nested;
                }
            }
        }
    }

    public override void Render(DrawingContext drawingContext)
    {
        if (TextView == null || !TextView.VisualLinesValid) return;

        var steps = GetAllSteps(_viewModel.Steps).ToList();
        if (steps.Count == 0) return;

        // Subtle divider line on the right side of the gutter
        drawingContext.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(60, 64, 67)), 1), new Point(Bounds.Width - 1, 0), new Point(Bounds.Width - 1, Bounds.Height));

        foreach (var visualLine in TextView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            var step = steps.FirstOrDefault(s => s.StartLine == lineNumber);
            if (step == null) continue;

            double yTop = visualLine.GetVisualPosition(0, VisualYPosition.TextTop).Y - TextView.VerticalOffset;
            double yBottom = visualLine.GetVisualPosition(0, VisualYPosition.TextBottom).Y - TextView.VerticalOffset;
            double cy = yTop + (yBottom - yTop) / 2.0;

            DrawIndicator(drawingContext, step, lineNumber, cy);
        }
    }

    private void DrawIndicator(DrawingContext drawingContext, TestStudioStepModel step, int lineNumber, double cy)
    {
        double cx = 11; // Center of the 24px margin

        if (_viewModel.IsExecuting)
        {
            // Playback Mode status rendering
            switch (step.Status)
            {
                case StepStatus.Running:
                    // Pulse or solid blue circle
                    drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(26, 115, 232)), null, new Point(cx, cy), 5, 5);
                    break;
                case StepStatus.Passed:
                    // Green checkmark
                    var greenBrush = new SolidColorBrush(Color.FromRgb(15, 157, 88));
                    var checkPen = new Pen(greenBrush, 2.0);
                    drawingContext.DrawLine(checkPen, new Point(cx - 4, cy), new Point(cx - 1, cy + 3));
                    drawingContext.DrawLine(checkPen, new Point(cx - 1, cy + 3), new Point(cx + 4, cy - 3));
                    break;
                case StepStatus.Failed:
                    // Red cross
                    var redBrush = new SolidColorBrush(Color.FromRgb(197, 34, 31));
                    var crossPen = new Pen(redBrush, 2.0);
                    drawingContext.DrawLine(crossPen, new Point(cx - 3, cy - 3), new Point(cx + 3, cy + 3));
                    drawingContext.DrawLine(crossPen, new Point(cx - 3, cy + 3), new Point(cx + 3, cy - 3));
                    break;
                case StepStatus.Pending:
                default:
                    // Small gray bullet
                    drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(128, 128, 128)), null, new Point(cx, cy), 3, 3);
                    break;
            }
        }
        else if (_recorderViewModel.IsRecording)
        {
            // Recording Mode status rendering (red dot)
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(219, 68, 85)), null, new Point(cx, cy), 4, 4);
        }
        else
        {
            // Normal / Design Mode (Play button triangle with hover feedback)
            var path = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(cx - 3, cy - 5), IsClosed = true };
            figure.Segments.Add(new LineSegment { Point = new Point(cx - 3, cy + 5) });
            figure.Segments.Add(new LineSegment { Point = new Point(cx + 5, cy) });
            path.Figures.Add(figure);

            var brush = (lineNumber == _hoveredLineNumber)
                ? new SolidColorBrush(Color.FromRgb(26, 115, 232)) // Highlight blue
                : new SolidColorBrush(Color.FromRgb(150, 150, 150), 0.7); // Neutral semi-transparent gray

            drawingContext.DrawGeometry(brush, null, path);
        }
    }
}
