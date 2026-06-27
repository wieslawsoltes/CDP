#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using XamlPlayground.Editor.Minimap.Inline;

namespace CdpInspectorApp.Services;

public class TestStudioGutterDataProvider : IGutterMarginDataProvider, IDisposable
{
    private readonly TestStudioViewModel _viewModel;
    private readonly RecorderViewModel _recorderViewModel;
    private readonly HashSet<TestStudioStepModel> _subscribedSteps = new();

    public event EventHandler? VisualInvalidated;

    public TestStudioGutterDataProvider(TestStudioViewModel viewModel, RecorderViewModel recorderViewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _recorderViewModel = recorderViewModel ?? throw new ArgumentNullException(nameof(recorderViewModel));

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _recorderViewModel.PropertyChanged += RecorderViewModel_PropertyChanged;
        _viewModel.Steps.CollectionChanged += Steps_CollectionChanged;

        SubscribeAll();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioViewModel.IsExecuting) ||
            e.PropertyName == nameof(TestStudioViewModel.YamlCode))
        {
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
        else if (e.PropertyName == nameof(TestStudioViewModel.Steps))
        {
            UnsubscribeAll();
            _viewModel.Steps.CollectionChanged -= Steps_CollectionChanged;
            _viewModel.Steps.CollectionChanged += Steps_CollectionChanged;
            SubscribeAll();
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RecorderViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecorderViewModel.IsRecording))
        {
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TestStudioStepModel oldStep in e.OldItems)
            {
                UnsubscribeStep(oldStep);
            }
        }
        if (e.NewItems != null)
        {
            foreach (TestStudioStepModel newStep in e.NewItems)
            {
                SubscribeStep(newStep);
            }
        }
        VisualInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioStepModel.Status) ||
            e.PropertyName == nameof(TestStudioStepModel.StartLine))
        {
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NestedSteps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TestStudioStepModel oldStep in e.OldItems)
            {
                UnsubscribeStep(oldStep);
            }
        }
        if (e.NewItems != null)
        {
            foreach (TestStudioStepModel newStep in e.NewItems)
            {
                SubscribeStep(newStep);
            }
        }
        VisualInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void SubscribeStep(TestStudioStepModel step)
    {
        if (step == null) return;
        if (_subscribedSteps.Add(step))
        {
            step.PropertyChanged += Step_PropertyChanged;
        }
        if (step.NestedSteps != null)
        {
            step.NestedSteps.CollectionChanged += NestedSteps_CollectionChanged;
            foreach (var nested in step.NestedSteps)
            {
                SubscribeStep(nested);
            }
        }
    }

    private void UnsubscribeStep(TestStudioStepModel step)
    {
        if (step == null) return;
        if (_subscribedSteps.Remove(step))
        {
            step.PropertyChanged -= Step_PropertyChanged;
        }
        if (step.NestedSteps != null)
        {
            step.NestedSteps.CollectionChanged -= NestedSteps_CollectionChanged;
            foreach (var nested in step.NestedSteps)
            {
                UnsubscribeStep(nested);
            }
        }
    }

    private void SubscribeAll()
    {
        foreach (var step in _viewModel.Steps)
        {
            SubscribeStep(step);
        }
    }

    private void UnsubscribeAll()
    {
        var stepsToUnsubscribe = _subscribedSteps.ToList();
        foreach (var step in stepsToUnsubscribe)
        {
            UnsubscribeStep(step);
        }
    }

    public void RenderIndicator(DrawingContext drawingContext, int lineNumber, double cy, double width, double height, bool isHovered)
    {
        var steps = GetAllSteps(_viewModel.Steps).ToList();
        var step = steps.FirstOrDefault(s => s.StartLine == lineNumber);
        if (step == null) return;

        double cx = 11; // Center of the 24px margin

        if (_viewModel.IsExecuting)
        {
            switch (step.Status)
            {
                case StepStatus.Running:
                    drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(26, 115, 232)), null, new Point(cx, cy), 5, 5);
                    break;
                case StepStatus.Passed:
                    var greenBrush = new SolidColorBrush(Color.FromRgb(15, 157, 88));
                    var checkPen = new Pen(greenBrush, 2.0);
                    drawingContext.DrawLine(checkPen, new Point(cx - 4, cy), new Point(cx - 1, cy + 3));
                    drawingContext.DrawLine(checkPen, new Point(cx - 1, cy + 3), new Point(cx + 4, cy - 3));
                    break;
                case StepStatus.Failed:
                    var redBrush = new SolidColorBrush(Color.FromRgb(197, 34, 31));
                    var crossPen = new Pen(redBrush, 2.0);
                    drawingContext.DrawLine(crossPen, new Point(cx - 3, cy - 3), new Point(cx + 3, cy + 3));
                    drawingContext.DrawLine(crossPen, new Point(cx - 3, cy + 3), new Point(cx + 3, cy - 3));
                    break;
                case StepStatus.Pending:
                default:
                    drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(128, 128, 128)), null, new Point(cx, cy), 3, 3);
                    break;
            }
        }
        else if (_recorderViewModel.IsRecording)
        {
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromRgb(219, 68, 85)), null, new Point(cx, cy), 4, 4);
        }
        else
        {
            var path = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(cx - 3, cy - 5), IsClosed = true };
            figure.Segments.Add(new LineSegment { Point = new Point(cx - 3, cy + 5) });
            figure.Segments.Add(new LineSegment { Point = new Point(cx + 5, cy) });
            path.Figures.Add(figure);

            var brush = isHovered
                ? new SolidColorBrush(Color.FromRgb(26, 115, 232))
                : new SolidColorBrush(Color.FromRgb(150, 150, 150), 0.7);

            drawingContext.DrawGeometry(brush, null, path);
        }
    }

    public void OnLineClicked(int lineNumber)
    {
        if (_viewModel.IsExecuting || _recorderViewModel.IsRecording) return;

        var steps = GetAllSteps(_viewModel.Steps).ToList();
        var step = steps.FirstOrDefault(s => s.StartLine == lineNumber);
        if (step != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _viewModel.RunSingleStepAsync(step);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TestStudioGutterDataProvider] Single-step execution failed: {ex.Message}");
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

    public void Dispose()
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _recorderViewModel.PropertyChanged -= RecorderViewModel_PropertyChanged;
        _viewModel.Steps.CollectionChanged -= Steps_CollectionChanged;
        UnsubscribeAll();
    }
}
