using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class OverlayDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "setInspectMode":
                {
                    string mode = @params["mode"]?.GetValue<string>() ?? "none";
                    bool enabled = mode != "none";
                    if (session.CurrentTargetSession != null)
                    {
                        session.CurrentTargetSession.InspectModeEnabled = enabled;
                    }
                    return new JsonObject();
                }

            case "highlightNode":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();

                    UIElement? targetVisual = null;
                    if (nodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(nodeId.Value) as UIElement;
                    }
                    else if (backendNodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(backendNodeId.Value) as UIElement;
                    }

                    if (targetVisual != null && session.Window != null)
                    {
                        HighlightOverlayManager.ShowHighlight(session.Window, targetVisual);
                    }
                    return new JsonObject();
                }

            case "hideHighlight":
                {
                    if (session.Window != null)
                    {
                        HighlightOverlayManager.HideHighlight(session.Window);
                    }
                    return new JsonObject();
                }

            case "setShowPaintRects":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Overlay.{action} is not implemented");
        }
    }
}
