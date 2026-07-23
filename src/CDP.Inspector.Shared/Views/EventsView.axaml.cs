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

        var eventsPanel = this.FindControl<Control>("lstEvents");
        var payloadPanel = this.FindControl<Control>("EventsPayloadPanel");
        var hiddenPanel = this.FindControl<Panel>("HiddenPanel");
        var splitControl = this.FindControl<SuperSplit>("SplitControl");

        hiddenPanel?.Children.Clear();

        if (eventsPanel != null) _viewsCache["EventsList"] = eventsPanel;
        if (payloadPanel != null) _viewsCache["EventsPayload"] = payloadPanel;

        if (splitControl != null)
        {
            splitControl.ViewResolver = (viewName, targetBox) =>
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
    }
}
