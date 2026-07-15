using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpGalleryApp.Views;

public partial class AccordionPage : UserControl
{
    public AccordionPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
