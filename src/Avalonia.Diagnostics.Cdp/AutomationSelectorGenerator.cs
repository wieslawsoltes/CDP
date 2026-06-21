using System.Collections.Generic;
using System.Linq;
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
            
            string part = current.GetType().Name;
            var parent = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
            if (parent != null)
            {
                var siblings = (useLogicalTree ? SelectorEngine.GetLogicalChildren(parent) : parent.GetVisualChildren()).ToList();
                int sameTypeCount = 0;
                foreach (var sib in siblings)
                {
                    if (sib.GetType().Name == part)
                    {
                        sameTypeCount++;
                    }
                }
                if (sameTypeCount > 1)
                {
                    int actualIndex = siblings.IndexOf(current) + 1;
                    part += $":nth-child({actualIndex})";
                }
            }

            pathParts.Insert(0, part);
            current = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
        }

        return _fallback.GenerateSelector(visual, useLogicalTree);
    }
}
