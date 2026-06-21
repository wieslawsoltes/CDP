using System;
using System.Collections.Generic;
using System.Linq;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public class DomClientSelectorGenerator : IClientSelectorGenerator
{
    private string GetSimpleSelector(DomNodeModel node)
    {
        string part = node.NodeName;
        var classAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Class", StringComparison.OrdinalIgnoreCase));
        if (classAttr != null && !string.IsNullOrEmpty(classAttr.Value))
        {
            var firstClass = classAttr.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstClass) && !firstClass.StartsWith(":"))
            {
                part += "." + firstClass;
            }
        }
        return part;
    }

    public string GenerateSelector(DomNodeModel node)
    {
        // 1. Walk up to find if there is any named ancestor (has id/Name attribute)
        DomNodeModel? current = node;
        var pathParts = new List<string>();
        while (current != null)
        {
            var idAttr = current.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
            if (idAttr != null && !string.IsNullOrEmpty(idAttr.Value) && !idAttr.Value.StartsWith("PART_"))
            {
                pathParts.Insert(0, $"#{idAttr.Value}");
                return string.Join(" > ", pathParts);
            }

            string part = GetSimpleSelector(current);
            var parent = current.Parent;
            if (parent != null)
            {
                var siblings = parent.Children;
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
            current = current.Parent;
        }

        // 2. Fallback to structural path if no named ancestor is found
        var parts = new List<string>();
        current = node;
        while (current != null)
        {
            string part = GetSimpleSelector(current);
            var parent = current.Parent;
            if (parent != null)
            {
                var siblings = parent.Children;
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
            current = current.Parent;
        }

        return string.Join(" > ", parts);
    }
}
