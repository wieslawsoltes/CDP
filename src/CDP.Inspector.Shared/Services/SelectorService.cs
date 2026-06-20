using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public class SelectorService
{
    private static readonly SelectorService _instance = new();
    public static SelectorService Instance => _instance;

    public ObservableCollection<string> AvailableSelectors { get; } = new();

    public void UpdateSelectors(DomNodeModel? rootNode)
    {
        var selectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectSelectors(rootNode, selectors);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AvailableSelectors.Clear();
            foreach (var sel in selectors.OrderBy(s => s))
            {
                AvailableSelectors.Add(sel);
            }
        });
    }

    private void CollectSelectors(DomNodeModel? node, HashSet<string> selectors)
    {
        if (node == null) return;

        if (!node.NodeName.StartsWith("#") && !string.IsNullOrEmpty(node.NodeName))
        {
            string tagName = node.NodeName;
            selectors.Add(tagName);

            var idAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
            if (idAttr != null && !string.IsNullOrEmpty(idAttr.Value))
            {
                selectors.Add($"#{idAttr.Value}");
                selectors.Add($"{tagName}#{idAttr.Value}");
            }

            var classAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Class", StringComparison.OrdinalIgnoreCase));
            if (classAttr != null && !string.IsNullOrEmpty(classAttr.Value))
            {
                foreach (var singleClass in classAttr.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    selectors.Add($".{singleClass}");
                    selectors.Add($"{tagName}.{singleClass}");
                }
            }
        }

        foreach (var child in node.Children)
        {
            CollectSelectors(child, selectors);
        }
    }
}
