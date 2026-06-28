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

        try
        {
            if (parameters.ContainsKey("ForceExceptionTest"))
            {
                throw new InvalidOperationException("Forced layout exception for testing.");
            }

            var graph = new GeometryGraph();

            // 1. Create nodes
            var nodeMap = new Dictionary<NodeViewModel, Microsoft.Msagl.Core.Layout.Node>();
            foreach (var vmNode in viewModel.Nodes)
            {
                if (vmNode is GroupNodeViewModel) continue;

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
            double nodeSeparation = 40.0;
            if (parameters.TryGetValue("NodeSeparation", out var nsVal) && nsVal is double nsD) nodeSeparation = nsD;
            else if (parameters.TryGetValue("NodeSeparation", out var nsVal2) && nsVal2 is int nsI) nodeSeparation = nsI;

            double layerSeparation = 60.0;
            if (parameters.TryGetValue("LayerSeparation", out var lsVal) && lsVal is double lsD) layerSeparation = lsD;
            else if (parameters.TryGetValue("LayerSeparation", out var lsVal2) && lsVal2 is int lsI) layerSeparation = lsI;

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

            // 5. Shift coordinates & recalculate group bounds
            ApplyCalculatedPositions(graph, viewModel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAGL layout setup or execution failed ({ex.Message}). Attempting fallback layout.");

            string algorithm = parameters.TryGetValue("Algorithm", out var algVal) ? (algVal as string ?? "Sugiyama") : "Sugiyama";
            double nodeSeparation = 40.0;
            if (parameters.TryGetValue("NodeSeparation", out var nsVal) && nsVal is double nsD) nodeSeparation = nsD;
            double layerSeparation = 60.0;
            if (parameters.TryGetValue("LayerSeparation", out var lsVal) && lsVal is double lsD) layerSeparation = lsD;
            string direction = parameters.TryGetValue("Direction", out var dirVal) ? (dirVal as string ?? "Horizontal") : "Horizontal";

            if (algorithm != "Sugiyama")
            {
                try
                {
                    // Rebuild a clean graph for Sugiyama fallback
                    var fallbackGraph = new GeometryGraph();
                    var nodeMap = new Dictionary<NodeViewModel, Microsoft.Msagl.Core.Layout.Node>();
                    foreach (var vmNode in viewModel.Nodes)
                    {
                        if (vmNode is GroupNodeViewModel) continue;
                        var msaglNode = new Microsoft.Msagl.Core.Layout.Node();
                        msaglNode.BoundaryCurve = CurveFactory.CreateRectangle(vmNode.Width, vmNode.Height, new Point(0, 0));
                        msaglNode.UserData = vmNode;
                        fallbackGraph.Nodes.Add(msaglNode);
                        nodeMap[vmNode] = msaglNode;
                    }
                    foreach (var conn in viewModel.Connections)
                    {
                        if (conn.FromNode != null && conn.ToNode != null &&
                            nodeMap.TryGetValue(conn.FromNode, out var fromNode) &&
                            nodeMap.TryGetValue(conn.ToNode, out var toNode))
                        {
                            var edge = new Microsoft.Msagl.Core.Layout.Edge(fromNode, toNode);
                            fallbackGraph.Edges.Add(edge);
                        }
                    }

                    var fallbackSugSettings = new SugiyamaLayoutSettings();
                    fallbackSugSettings.NodeSeparation = nodeSeparation;
                    fallbackSugSettings.LayerSeparation = layerSeparation;
                    fallbackSugSettings.Transformation = direction == "Horizontal" 
                        ? PlaneTransformation.Rotation(Math.PI / 2.0) 
                        : PlaneTransformation.UnitTransformation;
                    LayoutHelpers.CalculateLayout(fallbackGraph, fallbackSugSettings, null);

                    ApplyCalculatedPositions(fallbackGraph, viewModel);
                }
                catch
                {
                    FallbackToSimpleHorizontal(viewModel);
                }
            }
            else
            {
                FallbackToSimpleHorizontal(viewModel);
            }
        }
    }

    private void ApplyCalculatedPositions(GeometryGraph graph, NodeEditorViewModel viewModel)
    {
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

        // Recalculate bounds for GroupNodeViewModels
        var groupNodes = viewModel.Nodes.OfType<GroupNodeViewModel>().ToList();
        foreach (var group in groupNodes)
        {
            if (group.ChildNodeIds.Count == 0) continue;

            var children = viewModel.Nodes.Where(n => group.ChildNodeIds.Contains(n.Id)).ToList();
            if (children.Count == 0) continue;

            double padding = 20.0;
            double minChildX = children.Min(n => n.X);
            double minChildY = children.Min(n => n.Y);
            double maxChildX = children.Max(n => n.X + n.Width);
            double maxChildY = children.Max(n => n.Y + n.Height);

            double headerHeight = 30.0;
            group.X = minChildX - padding;
            group.Y = minChildY - padding - headerHeight;
            group.Width = maxChildX - minChildX + padding * 2.0;
            group.Height = maxChildY - minChildY + padding * 2.0 + headerHeight;
        }
    }

    private void FallbackToSimpleHorizontal(NodeEditorViewModel viewModel)
    {
        var fallback = new SimpleHorizontalLayoutProvider();
        fallback.ApplyLayout(viewModel, new Dictionary<string, object>());
    }
}
