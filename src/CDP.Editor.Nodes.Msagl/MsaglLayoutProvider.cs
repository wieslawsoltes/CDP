#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Miscellaneous;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Layout.Incremental;
using CDP.Editor.Nodes.ViewModels;

namespace CDP.Editor.Nodes.Msagl;

public class MsaglLayoutProvider : INodeLayoutProvider
{
    public string Name => "Microsoft MSAGL";

    public void ApplyLayout(NodeEditorViewModel viewModel, Dictionary<string, object> parameters)
    {
        if (viewModel.Nodes.Count == 0) return;

        var graph = new GeometryGraph();

        // 1. Create nodes
        var nodeMap = new Dictionary<NodeViewModel, Microsoft.Msagl.Core.Layout.Node>();
        foreach (var vmNode in viewModel.Nodes)
        {
            var msaglNode = new Microsoft.Msagl.Core.Layout.Node();
            msaglNode.BoundaryCurve = CurveFactory.CreateRectangle(vmNode.Width, vmNode.Height, new Point(0, 0));
            msaglNode.UserData = vmNode;
            
            graph.Nodes.Add(msaglNode);
            nodeMap[vmNode] = msaglNode;
        }

        // 2. Create edges
        foreach (var conn in viewModel.Connections)
        {
            if (conn.FromNode != null && conn.ToNode != null &&
                nodeMap.TryGetValue(conn.FromNode, out var fromNode) &&
                nodeMap.TryGetValue(conn.ToNode, out var toNode))
            {
                var edge = new Microsoft.Msagl.Core.Layout.Edge(fromNode, toNode);
                graph.Edges.Add(edge);
            }
        }

        // 3. Configure settings
        LayoutAlgorithmSettings settings;

        string algorithm = parameters.TryGetValue("Algorithm", out var algVal) ? (algVal as string ?? "Sugiyama") : "Sugiyama";
        double nodeSeparation = parameters.TryGetValue("NodeSeparation", out var nsVal) ? (double)nsVal : 40.0;
        double layerSeparation = parameters.TryGetValue("LayerSeparation", out var lsVal) ? (double)lsVal : 60.0;
        string direction = parameters.TryGetValue("Direction", out var dirVal) ? (dirVal as string ?? "Horizontal") : "Horizontal";

        if (algorithm == "Mds")
        {
            var mds = new MdsLayoutSettings();
            mds.NodeSeparation = nodeSeparation;
            settings = mds;
        }
        else if (algorithm == "FastIncremental")
        {
            var fi = new FastIncrementalLayoutSettings();
            fi.NodeSeparation = nodeSeparation;
            settings = fi;
        }
        else // Sugiyama
        {
            var sug = new SugiyamaLayoutSettings();
            sug.NodeSeparation = nodeSeparation;
            sug.LayerSeparation = layerSeparation;
            sug.Transformation = direction == "Horizontal" 
                ? PlaneTransformation.Rotation(Math.PI / 2.0) 
                : PlaneTransformation.UnitTransformation;
            settings = sug;
        }

        // 4. Run MSAGL layout
        LayoutHelpers.CalculateLayout(graph, settings, null);

        // 5. Shift layout so it starts at (20, 20) in canvas coordinates
        double minX = double.MaxValue;
        double minY = double.MaxValue;

        var tempPositions = new Dictionary<NodeViewModel, (double x, double y)>();
        foreach (var msaglNode in graph.Nodes)
        {
            var vmNode = (NodeViewModel)msaglNode.UserData;
            double x = msaglNode.Center.X - vmNode.Width / 2.0;
            double y = msaglNode.Center.Y - vmNode.Height / 2.0;
            
            tempPositions[vmNode] = (x, y);

            if (x < minX) minX = x;
            if (y < minY) minY = y;
        }

        double targetMarginX = 20.0;
        double targetMarginY = 20.0;
        double offsetX = targetMarginX - minX;
        double offsetY = targetMarginY - minY;

        foreach (var msaglNode in graph.Nodes)
        {
            var vmNode = (NodeViewModel)msaglNode.UserData;
            var (x, y) = tempPositions[vmNode];
            vmNode.X = x + offsetX;
            vmNode.Y = y + offsetY;
        }
    }
}
