using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp;

public class AutomationSelectorGenerator : ISelectorGenerator
{
    private readonly ISelectorGenerator _fallback = new DomSelectorGenerator();

    public string GenerateSelector(Visual visual, bool useLogicalTree = false)
    {
        Visual? current = visual;
        var pathParts = new List<string>();
        while (current != null)
        {
            if (current is Control ctrl)
            {
                var accessId = ctrl.GetValue(AutomationProperties.AutomationIdProperty) as string;
                if (!string.IsNullOrEmpty(accessId))
                {
                    pathParts.Insert(0, $"[AccessibilityId=\"{accessId}\"]");
                    return string.Join(" > ", pathParts);
                }
            }
            pathParts.Insert(0, current.GetType().Name);
            current = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
        }

        return _fallback.GenerateSelector(visual, useLogicalTree);
    }
}
