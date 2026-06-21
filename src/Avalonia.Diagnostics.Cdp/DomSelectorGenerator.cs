using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp;

public class DomSelectorGenerator : ISelectorGenerator
{
    private string GetSimpleSelector(Visual visual)
    {
        string part = visual.GetType().Name;
        if (visual is Control ctrl)
        {
            var validClasses = ctrl.Classes.Where(cls => !cls.StartsWith(":")).ToList();
            if (validClasses.Count > 0)
            {
                part += "." + string.Join(".", validClasses);
            }
        }
        return part;
    }

    public string GenerateSelector(Visual visual, bool useLogicalTree = false)
    {
        // Walk up to find if there is any named ancestor (ignoring PART_ names)
        Visual? current = visual;
        var pathParts = new List<string>();
        while (current != null)
        {
            if (current is Control ctrl && !string.IsNullOrEmpty(ctrl.Name) && !ctrl.Name.StartsWith("PART_"))
            {
                pathParts.Insert(0, $"#{ctrl.Name}");
                return string.Join(" > ", pathParts);
            }

            string part = GetSimpleSelector(current);
            var parent = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
            if (parent != null)
            {
                var siblings = (useLogicalTree ? SelectorEngine.GetLogicalChildren(parent) : parent.GetVisualChildren()).ToList();
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
            current = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
        }

        // Fallback to structural path if no named ancestor is found
        var parts = new List<string>();
        current = visual;
        while (current != null)
        {
            string part = GetSimpleSelector(current);
            var parent = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
            if (parent != null)
            {
                var siblings = (useLogicalTree ? SelectorEngine.GetLogicalChildren(parent) : parent.GetVisualChildren()).ToList();
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
            current = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
        }

        return string.Join(" > ", parts);
    }
}
