using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpGalleryApp.Views;

public partial class HtmlPage : UserControl
{
    public HtmlPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
