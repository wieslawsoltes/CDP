using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public class DomSelectorGenerator : ISelectorGenerator
{
    private string GetSimpleSelector(Visual visual)
    {
        return visual.GetType().Name;
    }

    public string GenerateSelector(Visual visual, bool useLogicalTree = false)
    {
        if (visual is FrameworkElement startCtrl)
        {
            if (!string.IsNullOrEmpty(startCtrl.Name) && !startCtrl.Name.StartsWith("PART_"))
            {
                return $"#{startCtrl.Name}";
            }

            var accessId = AutomationProperties.GetAutomationId(startCtrl);
            if (!string.IsNullOrEmpty(accessId))
            {
                return $"[AccessibilityId=\"{accessId}\"]";
            }
        }

        string targetPart = GetSimpleSelector(visual);
        var text = SelectorEngine.GetVisualTextContent(visual);
        if (!string.IsNullOrEmpty(text) && text.Length <= 60 && !text.Contains('\n') && !text.Contains('\r'))
        {
            var escaped = text.Replace("\"", "\\\"");
            targetPart += $":contains(\"{escaped}\")";
        }

        Visual? current = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
        var pathParts = new List<string> { targetPart };
        while (current != null)
        {
            if (current is FrameworkElement ctrl && !string.IsNullOrEmpty(ctrl.Name) && !ctrl.Name.StartsWith("PART_"))
            {
                pathParts.Insert(0, $"#{ctrl.Name}");
                return string.Join(" > ", pathParts);
            }

            string part = GetSimpleSelector(current);
            var parent = CdpVisualTreeHelper.GetParent(current, useLogicalTree);
            if (parent != null)
            {
                var siblings = CdpVisualTreeHelper.GetChildren(parent, useLogicalTree).ToList();
                int sameTypeCount = 0;
                foreach (var sib in siblings)
                {
                    if (GetSimpleSelector(sib) == part)
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
            current = CdpVisualTreeHelper.GetParent(current, useLogicalTree);
        }

        var parts = new List<string> { targetPart };
        current = CdpVisualTreeHelper.GetParent(visual, useLogicalTree);
        while (current != null)
        {
            string part = GetSimpleSelector(current);
            var parent = CdpVisualTreeHelper.GetParent(current, useLogicalTree);
            if (parent != null)
            {
                var siblings = CdpVisualTreeHelper.GetChildren(parent, useLogicalTree).ToList();
                int sameTypeCount = 0;
                foreach (var sib in siblings)
                {
                    if (GetSimpleSelector(sib) == part)
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

            parts.Insert(0, part);
            current = CdpVisualTreeHelper.GetParent(current, useLogicalTree);
        }

        return string.Join(" > ", parts);
    }
}
