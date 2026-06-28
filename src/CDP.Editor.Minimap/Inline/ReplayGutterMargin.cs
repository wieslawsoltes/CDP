using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

public interface IGutterMarginDataProvider
{
    event EventHandler? VisualInvalidated;
    void RenderIndicator(DrawingContext drawingContext, int lineNumber, double yCenter, double width, double height, bool isHovered);
    void OnLineClicked(int lineNumber);
}

public class ReplayGutterMargin : AbstractMargin
{
    private readonly IGutterMarginDataProvider _dataProvider;
    private int _hoveredLineNumber = -1;

    public ReplayGutterMargin(IGutterMarginDataProvider dataProvider)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _dataProvider.VisualInvalidated += DataProvider_VisualInvalidated;

        this.DetachedFromVisualTree += (s, e) =>
        {
            _dataProvider.VisualInvalidated -= DataProvider_VisualInvalidated;
            if (_dataProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
    }

    private void DataProvider_VisualInvalidated(object? sender, EventArgs e)
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
        if (TextView == null || !TextView.VisualLinesValid) return;

        var pos = e.GetPosition(TextView);
        var visualLine = TextView.GetVisualLineFromVisualTop(pos.Y + TextView.VerticalOffset);
        if (visualLine == null) return;
        int lineNumber = visualLine.FirstDocumentLine.LineNumber;

        e.Handled = true;
        _dataProvider.OnLineClicked(lineNumber);
    }

    public override void Render(DrawingContext drawingContext)
    {
        if (TextView == null || !TextView.VisualLinesValid) return;

        // Subtle divider line on the right side of the gutter
        drawingContext.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(60, 64, 67)), 1), new Point(Bounds.Width - 1, 0), new Point(Bounds.Width - 1, Bounds.Height));

        foreach (var visualLine in TextView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            bool isHovered = (lineNumber == _hoveredLineNumber);

            double yTop = visualLine.GetVisualPosition(0, VisualYPosition.TextTop).Y - TextView.VerticalOffset;
            double yBottom = visualLine.GetVisualPosition(0, VisualYPosition.TextBottom).Y - TextView.VerticalOffset;
            double cy = yTop + (yBottom - yTop) / 2.0;

            _dataProvider.RenderIndicator(drawingContext, lineNumber, cy, Bounds.Width, yBottom - yTop, isHovered);
        }
    }
}
