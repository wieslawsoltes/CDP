using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;

namespace WinUI.Diagnostics.Cdp;

public class AutomationSelectorGenerator : ISelectorGenerator
{
    public string? Generate(DependencyObject control)
    {
        string id = AutomationProperties.GetAutomationId(control);
        if (!string.IsNullOrEmpty(id))
        {
            return $"[AutomationId=\"{id}\"]";
        }
        return null;
    }
}
