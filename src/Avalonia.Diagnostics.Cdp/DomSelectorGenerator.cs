using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp;

public class DomSelectorGenerator : ISelectorGenerator
{
    public string GenerateSelector(Visual visual, bool useLogicalTree = false)
    {
        if (visual is Control c && !string.IsNullOrEmpty(c.Name) && !c.Name.StartsWith("PART_"))
        {
            return $"#{c.Name}";
        }

        // Walk up to find if there is any named ancestor (ignoring PART_ names)
        Visual? current = visual;
        while (current != null)
        {
            if (current is Control ctrl && !string.IsNullOrEmpty(ctrl.Name) && !ctrl.Name.StartsWith("PART_"))
            {
                return $"#{ctrl.Name}";
            }
            current = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
        }

        // Fallback to structural path if no named ancestor is found
        var parts = new List<string>();
        current = visual;
        while (current != null)
        {
            string part = current.GetType().Name;
            if (current is Control ctrlWithClasses)
            {
                var validClasses = ctrlWithClasses.Classes.Where(cls => !cls.StartsWith(":")).ToList();
                if (validClasses.Count > 0)
                {
                    part += "." + string.Join(".", validClasses);
                }
            }

            parts.Insert(0, part);
            current = useLogicalTree ? SelectorEngine.GetLogicalParent(current) : current.GetVisualParent();
        }

        return string.Join(" > ", parts);
    }
}
