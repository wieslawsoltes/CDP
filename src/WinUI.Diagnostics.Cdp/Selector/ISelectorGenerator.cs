using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp;

public interface ISelectorGenerator
{
    string? Generate(DependencyObject control);
}
