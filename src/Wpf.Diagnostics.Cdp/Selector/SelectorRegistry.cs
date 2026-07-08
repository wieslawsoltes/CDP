using System;
using System.Collections.Generic;

namespace Wpf.Diagnostics.Cdp;

public static class SelectorRegistry
{
    private static readonly Dictionary<string, ISelectorGenerator> s_generators = new(StringComparer.OrdinalIgnoreCase)
    {
        { "dom", new DomSelectorGenerator() },
        { "automation", new AutomationSelectorGenerator() }
    };

    public static ISelectorGenerator GetGenerator(string? mode)
    {
        if (string.IsNullOrEmpty(mode) || !s_generators.TryGetValue(mode, out var gen))
        {
            return s_generators["dom"];
        }
        return gen;
    }
}
