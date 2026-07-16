using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpInspectorApp.Views;

public partial class HtmlPreviewView : UserControl
{
    public HtmlPreviewView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
