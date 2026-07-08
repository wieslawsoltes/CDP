using System.Collections.Generic;

namespace WinUI.Diagnostics.Cdp;

public static class SelectorRegistry
{
    private static readonly List<ISelectorGenerator> _generators = new();

    static SelectorRegistry()
    {
        _generators.Add(new AutomationSelectorGenerator());
        _generators.Add(new DomSelectorGenerator());
    }

    public static IEnumerable<ISelectorGenerator> Generators => _generators;
}
