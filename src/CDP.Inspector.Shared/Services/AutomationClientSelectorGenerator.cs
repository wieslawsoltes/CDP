using System;
using System.Collections.Generic;
using System.Linq;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public class AutomationClientSelectorGenerator : IClientSelectorGenerator
{
    private readonly IClientSelectorGenerator _fallback = new DomClientSelectorGenerator();

    public string GenerateSelector(DomNodeModel node)
    {
        DomNodeModel? current = node;
        var pathParts = new List<string>();
        while (current != null)
        {
            var accessIdAttr = current.AttributesList.FirstOrDefault(a => a.Name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase));
            if (accessIdAttr != null && !string.IsNullOrEmpty(accessIdAttr.Value))
            {
                pathParts.Insert(0, $"[AccessibilityId=\"{accessIdAttr.Value}\"]");
                return string.Join(" > ", pathParts);
            }
            pathParts.Insert(0, current.NodeName);
            current = current.Parent;
        }

        return _fallback.GenerateSelector(node);
    }
}
