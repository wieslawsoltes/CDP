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
        var accessIdAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("AutomationId", StringComparison.OrdinalIgnoreCase));
        if (accessIdAttr != null && !string.IsNullOrEmpty(accessIdAttr.Value))
        {
            return $"[AccessibilityId=\"{accessIdAttr.Value}\"]";
        }

        var idAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
        if (idAttr != null && !string.IsNullOrEmpty(idAttr.Value) && !idAttr.Value.StartsWith("PART_"))
        {
            return $"#{idAttr.Value}";
        }

        string targetPart = node.NodeName;
        var text = ClientSelectorRegistry.GetNodeTextContent(node);
        string? escapedText = null;
        if (!string.IsNullOrEmpty(text) && text.Length <= 60 && !text.Contains('\n') && !text.Contains('\r'))
        {
            escapedText = text.Replace("\"", "\\\"");
            targetPart += $":contains(\"{escapedText}\")";
        }

        DomNodeModel? current = node.Parent;
        var pathParts = new List<string> { targetPart };
        while (current != null)
        {
            var curAccessIdAttr = current.AttributesList.FirstOrDefault(a => a.Name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("AutomationId", StringComparison.OrdinalIgnoreCase));
            if (curAccessIdAttr != null && !string.IsNullOrEmpty(curAccessIdAttr.Value))
            {
                pathParts.Insert(0, $"[AccessibilityId=\"{curAccessIdAttr.Value}\"]");
                return string.Join(" > ", pathParts);
            }
            pathParts.Insert(0, current.NodeName);
            current = current.Parent;
        }

        var fallbackSel = _fallback.GenerateSelector(node);
        if (escapedText != null)
        {
            return $"{fallbackSel}:contains(\"{escapedText}\")";
        }
        return fallbackSel;
    }
}
