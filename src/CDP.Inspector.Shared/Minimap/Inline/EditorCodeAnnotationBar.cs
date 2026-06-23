using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace XamlPlayground.Editor.Minimap.Inline;

internal static class EditorCodeAnnotationBar
{
    public static Control Create(Control owner, IReadOnlyList<EditorCodeAnnotation> annotations)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 1, 0, 1),
            VerticalAlignment = VerticalAlignment.Center
        };

        foreach (var annotation in annotations)
        {
            panel.Children.Add(CreateAnnotationControl(owner, annotation));
        }

        return new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(0, 0, 0, 1),
            Child = panel
        };
    }

    private static Control CreateAnnotationControl(Control owner, EditorCodeAnnotation annotation)
    {
        var actionBrush = EditorInlineTheme.Brush(
            owner,
            "DockTabSelectedForegroundBrush",
            new SolidColorBrush(Color.FromRgb(31, 95, 191)));
        var textBrush = EditorInlineTheme.Brush(
            owner,
            "DockChromeButtonForegroundBrush",
            new SolidColorBrush(Color.FromRgb(92, 92, 92)));

        if (annotation.Command is not null)
        {
            var button = new Button
            {
                Content = annotation.Text,
                Command = annotation.Command,
                CommandParameter = annotation.CommandParameter,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Foreground = actionBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrWhiteSpace(annotation.ToolTip))
            {
                ToolTip.SetTip(button, annotation.ToolTip);
            }

            return button;
        }

        var textBlock = new TextBlock
        {
            Text = annotation.Text,
            FontSize = 11,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(annotation.ToolTip))
        {
            ToolTip.SetTip(textBlock, annotation.ToolTip);
        }

        return textBlock;
    }
}
