using System;
using System.Collections.Generic;
using System.Linq;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public static class ClientSelectorRegistry
{
    private static readonly Dictionary<string, IClientSelectorGenerator> s_generators = new(StringComparer.OrdinalIgnoreCase)
    {
        { "dom", new DomClientSelectorGenerator() },
        { "automation", new AutomationClientSelectorGenerator() }
    };

    public static IClientSelectorGenerator GetGenerator(string? mode)
    {
        if (string.IsNullOrEmpty(mode) || !s_generators.TryGetValue(mode, out var gen))
        {
            return s_generators["dom"];
        }
        return gen;
    }

    public static string? GetNodeTextContent(DomNodeModel node)
    {
        var textAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("text", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Text", StringComparison.OrdinalIgnoreCase));
        if (textAttr != null && !string.IsNullOrEmpty(textAttr.Value))
        {
            return textAttr.Value;
        }

        if (node.NodeName == "#text" && !string.IsNullOrEmpty(node.NodeValue))
        {
            return node.NodeValue;
        }

        foreach (var child in node.Children)
        {
            var txt = GetNodeTextContent(child);
            if (!string.IsNullOrEmpty(txt)) return txt;
        }

        return null;
    }
}
