using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public class AutomationSelectorGenerator : ISelectorGenerator
{
    private readonly ISelectorGenerator _fallback = new DomSelectorGenerator();

    public string GenerateSelector(Visual visual, bool useLogicalTree = false)
    {
        if (visual is FrameworkElement startCtrl)
        {
            var accessId = AutomationProperties.GetAutomationId(startCtrl);
            if (!string.IsNullOrEmpty(accessId))
            {
                return $"[AccessibilityId=\"{accessId}\"]";
            }

            var name = startCtrl.Name;
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("PART_"))
            {
                return $"#{name}";
            }
        }

        string targetPart = visual.GetType().Name;
        var text = SelectorEngine.GetVisualTextContent(visual);
        if (!string.IsNullOrEmpty(text) && text.Length <= 60 && !text.Contains('\n') && !text.Contains('\r'))
        {
            var escaped = text.Replace("\"", "\\\"");
            targetPart += $":contains(\"{escaped}\")";
        }

        Visual? current = useLogicalTree ? SelectorEngine.GetLogicalParent(visual) : visual.GetVisualParent();
        var pathParts = new List<string> { targetPart };
        while (current != null)
        {
            if (current is FrameworkElement ctrl)
            {
                var accessId = AutomationProperties.GetAutomationId(ctrl);
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
