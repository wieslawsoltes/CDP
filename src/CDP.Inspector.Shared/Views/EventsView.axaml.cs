using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpInspectorApp.Views;

public partial class EventsView : UserControl
{
    public EventsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
