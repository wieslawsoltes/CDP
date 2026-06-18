using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;

namespace Avalonia;

public partial class CdpInspectorWindow : Window
{
    public CdpInspectorWindow()
    {
        InitializeComponent();
    }

    public CdpInspectorWindow(Window targetWindow, int port) : this()
    {
        if (targetWindow == null) throw new ArgumentNullException(nameof(targetWindow));

        // Auto-connect to the target window on load
        Dispatcher.UIThread.Post(async () =>
        {
            if (MainViewControl.DataContext is MainWindowViewModel vm)
            {
                try
                {
                    // Register the target window and get its target ID
                    var targetId = CdpServer.Register(targetWindow, targetWindow.Title ?? "Target Window");
                    var host = $"http://127.0.0.1:{port}";
                    var wsUrl = $"ws://127.0.0.1:{port}/devtools/page/{targetId}";

                    var targetItem = new TargetItem(targetWindow.Title ?? "Target Window", wsUrl, targetId);

                    // Set host address on connection view model
                    vm.Connection.HostAddress = host;

                    // Connect using CdpService
                    await vm.CdpService.ConnectAsync(host, targetItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CdpInspectorWindow] Auto-connect failed: {ex.Message}");
                }
            }
        });
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
}
