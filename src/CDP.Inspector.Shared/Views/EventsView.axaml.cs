using Avalonia.Controls;
using CDP.Editor.Splits.Controls;

namespace CdpInspectorApp.Views;

public partial class EventsView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    private void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }
        else if (control.Parent is SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
        }
    }

    public EventsView()
    {
        InitializeComponent();

        var eventsPanel = lstEvents;
        var payloadPanel = EventsPayloadPanel;

        HiddenPanel.Children.Clear();

        _viewsCache["EventsList"] = eventsPanel;
        _viewsCache["EventsPayload"] = payloadPanel;

        SplitControl.ViewResolver = (viewName, targetBox) =>
        {
            if (_viewsCache.TryGetValue(viewName, out var cached))
            {
                if (targetBox == null || cached.Parent != targetBox)
                {
                    DetachControl(cached);
                }
                return cached;
            }
            return new Control();
        };
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
