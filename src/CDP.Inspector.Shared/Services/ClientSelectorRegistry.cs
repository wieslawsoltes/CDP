using System;
using System.Collections.Generic;

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
}
