using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using CdpGalleryApp.ViewModels;
using CDP.Editor.Splits.Controls;

namespace CdpGalleryApp.Views;

public partial class SplitLayoutPage : UserControl
{
    public SplitLayoutPage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (DataContext is SplitLayoutPageViewModel vm && this.FindControl<SuperSplit>("SplitControl") is SuperSplit split)
            {
                split.ViewResolver = vm.ViewResolver;
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
