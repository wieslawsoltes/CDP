using System;
using System.Collections.Generic;
using System.Linq;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public class DomClientSelectorGenerator : IClientSelectorGenerator
{
    public string GenerateSelector(DomNodeModel node)
    {
        // 1. Walk up to find if there is any named ancestor (has id/Name attribute)
        DomNodeModel? current = node;
        while (current != null)
        {
            var idAttr = current.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
            if (idAttr != null && !string.IsNullOrEmpty(idAttr.Value) && !idAttr.Value.StartsWith("PART_"))
            {
                return $"#{idAttr.Value}";
            }
            current = current.Parent;
        }

        // 2. Fallback to structural path if no named ancestor is found
        var parts = new List<string>();
        current = node;
        while (current != null)
        {
            string part = current.NodeName;
            var classAttr = current.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Class", StringComparison.OrdinalIgnoreCase));
            if (classAttr != null && !string.IsNullOrEmpty(classAttr.Value))
            {
                var firstClass = classAttr.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstClass) && !firstClass.StartsWith(":"))
                {
                    part += "." + firstClass;
                }
            }
            parts.Insert(0, part);
            current = current.Parent;
        }

        return string.Join(" > ", parts);
    }
}
