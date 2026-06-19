using Avalonia.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = MainViewControl.DataContext;
    }

    public void LoadScriptContent(string content)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Recorder.LoadScriptContent(content);
        }
    }

    public int GetSelectedTreeTabIndex()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return vm.Elements.SelectedTreeTabIndex;
        }
        return -1;
    }

    public void SetSelectedTreeTabIndex(int index)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.SelectedTreeTabIndex = index;
        }
    }

    public int GetSelectedDomNodeId()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return vm.Elements.SelectedNode?.NodeId ?? -1;
        }
        return -1;
    }

    public string? GetSelectedAxNodeId()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return vm.Elements.SelectedAxNode?.NodeId;
        }
        return null;
    }

    public void SelectDomNodeById(int nodeId)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.SelectNodeById(nodeId);
        }
    }

    public void SetAxSearchQuery(string query)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.AxSearchQuery = query;
        }
    }

    public void ExecuteAxSearch()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.AxSearchCommand.Execute(null);
        }
    }

    public string? GetSelectedAxNodeRole()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return vm.Elements.SelectedAxNode?.Role;
        }
        return null;
    }

    public int FindDomNodeId(string selector)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return FindDomNodeId(vm.Elements.RootNodes, selector);
        }
        return -1;
    }

    private int FindDomNodeId(System.Collections.Generic.IEnumerable<CdpInspectorApp.Models.DomNodeModel> nodes, string selector)
    {
        foreach (var node in nodes)
        {
            if (selector.StartsWith("#"))
            {
                string targetId = selector.Substring(1);
                var idAttr = System.Linq.Enumerable.FirstOrDefault(node.AttributesList, a => a.Name.Equals("id", System.StringComparison.OrdinalIgnoreCase));
                if (idAttr != null && idAttr.Value.Equals(targetId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return node.NodeId;
                }
            }
            
            if (node.NodeName.Equals(selector, System.StringComparison.OrdinalIgnoreCase) || 
                node.DisplayName.Equals(selector, System.StringComparison.OrdinalIgnoreCase))
            {
                return node.NodeId;
            }

            int childId = FindDomNodeId(node.Children, selector);
            if (childId != -1) return childId;
        }
        return -1;
    }

    public void SetPropertySearchText(string text)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.PropertySearchText = text;
        }
    }

    public int GetFilteredPropertiesCount()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return System.Linq.Enumerable.Count(vm.Elements.FilteredProperties);
        }
        return 0;
    }

    public void SetCssSearchText(string text)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.CssSearchText = text;
        }
    }

    public int GetFilteredCssPropertiesCount()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return System.Linq.Enumerable.Count(vm.Elements.FilteredCssProperties);
        }
        return 0;
    }

    public void SetComputedSearchText(string text)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.ComputedSearchText = text;
        }
    }

    public int GetFilteredComputedStylesCount()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return System.Linq.Enumerable.Count(vm.Elements.FilteredComputedStyles);
        }
        return 0;
    }

    public void SetAttributeSearchText(string text)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Elements.AttributeSearchText = text;
        }
    }

    public int GetFilteredAttributesCount()
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            return System.Linq.Enumerable.Count(vm.Elements.FilteredAttributes);
        }
        return 0;
    }
}
