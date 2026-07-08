using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp;

public class DomSelectorGenerator : ISelectorGenerator
{
    public string? Generate(DependencyObject control)
    {
        if (control is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
        {
            return $"#{fe.Name}";
        }
        return null;
    }
}
