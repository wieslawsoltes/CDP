#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CDP.Editor.Nodes.ViewModels;
using CDP.Editor.Nodes.Msagl;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class NodeLayoutTests
{
    [Fact]
    public void SimpleHorizontalLayoutProvider_ArrangesNodesSequentially()
    {
        var vm = new NodeEditorViewModel();
        
        var node1 = vm.CreateNode("Node1", 10, 10);
        var node2 = vm.CreateNode("Node2", 10, 10);
        var node3 = vm.CreateNode("Node3", 10, 10);

        node1.Width = 100;
        node1.Height = 50;
        node2.Width = 100;
        node2.Height = 50;
        node3.Width = 100;
        node3.Height = 50;

        vm.ConnectNodes(node1, node2);
        vm.ConnectNodes(node2, node3);

        var provider = new SimpleHorizontalLayoutProvider();
        provider.ApplyLayout(vm, new Dictionary<string, object>());

        Assert.Equal(10.0, node1.X);
        Assert.Equal(210.0, node2.X);
        Assert.Equal(410.0, node3.X);
        Assert.Equal(20.0, node1.Y);
        Assert.Equal(20.0, node2.Y);
        Assert.Equal(20.0, node3.Y);
    }

    [Fact]
    public void MsaglLayoutProvider_SugiyamaHorizontal_NormalizesBoundsAndArranges()
    {
        var vm = new NodeEditorViewModel();

        var node1 = vm.CreateNode("Node1", 10, 10);
        var node2 = vm.CreateNode("Node2", 10, 10);
        var node3 = vm.CreateNode("Node3", 10, 10);

        node1.Width = 100;
        node1.Height = 50;
        node2.Width = 100;
        node2.Height = 50;
        node3.Width = 100;
        node3.Height = 50;

        vm.ConnectNodes(node1, node2);
        vm.ConnectNodes(node2, node3);

        var provider = new MsaglLayoutProvider();
        var parameters = new Dictionary<string, object>
        {
            { "Algorithm", "Sugiyama" },
            { "Direction", "Horizontal" },
            { "NodeSeparation", 40.0 },
            { "LayerSeparation", 60.0 }
        };

        provider.ApplyLayout(vm, parameters);

        // Verify that minimum coordinates are normalized to 20, 20 with floating point tolerance
        double minX = vm.Nodes.Min(n => n.X);
        double minY = vm.Nodes.Min(n => n.Y);

        Assert.Equal(20.0, minX, 0.0001);
        Assert.Equal(20.0, minY, 0.0001);

        // Under Horizontal Sugiyama, sequentially connected nodes should have increasing coordinates
        Assert.True(node2.X > node1.X, $"Expected node2.X ({node2.X}) > node1.X ({node1.X})");
        Assert.True(node3.X > node2.X, $"Expected node3.X ({node3.X}) > node2.X ({node2.X})");
    }

    [Fact]
    public void MsaglLayoutProvider_SugiyamaVertical_NormalizesBoundsAndArranges()
    {
        var vm = new NodeEditorViewModel();

        var node1 = vm.CreateNode("Node1", 10, 10);
        var node2 = vm.CreateNode("Node2", 10, 10);
        var node3 = vm.CreateNode("Node3", 10, 10);

        node1.Width = 100;
        node1.Height = 50;
        node2.Width = 100;
        node2.Height = 50;
        node3.Width = 100;
        node3.Height = 50;

        vm.ConnectNodes(node1, node2);
        vm.ConnectNodes(node2, node3);

        var provider = new MsaglLayoutProvider();
        var parameters = new Dictionary<string, object>
        {
            { "Algorithm", "Sugiyama" },
            { "Direction", "Vertical" },
            { "NodeSeparation", 40.0 },
            { "LayerSeparation", 60.0 }
        };

        provider.ApplyLayout(vm, parameters);

        double minX = vm.Nodes.Min(n => n.X);
        double minY = vm.Nodes.Min(n => n.Y);

        Assert.Equal(20.0, minX, 0.0001);
        Assert.Equal(20.0, minY, 0.0001);

        // Sequentially connected vertical nodes should be distributed vertically
        Assert.NotEqual(node1.Y, node2.Y, 0.0001);
        Assert.NotEqual(node2.Y, node3.Y, 0.0001);
    }

    [Fact]
    public void MsaglLayoutProvider_Mds_NormalizesBoundsAndArranges()
    {
        var vm = new NodeEditorViewModel();

        var node1 = vm.CreateNode("Node1", 10, 10);
        var node2 = vm.CreateNode("Node2", 10, 10);
        var node3 = vm.CreateNode("Node3", 10, 10);

        node1.Width = 100;
        node1.Height = 50;
        node2.Width = 100;
        node2.Height = 50;
        node3.Width = 100;
        node3.Height = 50;

        vm.ConnectNodes(node1, node2);
        vm.ConnectNodes(node2, node3);

        var provider = new MsaglLayoutProvider();
        var parameters = new Dictionary<string, object>
        {
            { "Algorithm", "Mds" },
            { "NodeSeparation", 50.0 }
        };

        provider.ApplyLayout(vm, parameters);

        double minX = vm.Nodes.Min(n => n.X);
        double minY = vm.Nodes.Min(n => n.Y);

        Assert.Equal(20.0, minX, 0.0001);
        Assert.Equal(20.0, minY, 0.0001);
    }
}
