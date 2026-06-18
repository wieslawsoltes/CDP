using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

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
                    session.InspectModeEnabled = (mode == "searchForNode");
                    return new JsonObject();
                }

            case "highlightNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual != null)
                    {
                        HighlightOverlayManager.ShowHighlight(session.Window, visual);
                    }
                    return new JsonObject();
                }

            case "hideHighlight":
                {
                    HighlightOverlayManager.HideHighlight(session.Window);
                    return new JsonObject();
                }

            case "highlightRect":
                {
                    return new JsonObject();
                }

            case "setShowDebugBorders":
            case "setShowFPSCounter":
            case "setShowPaintRects":
            case "setShowViewportSizeOnResize":
            case "setShowGridOverlays":
            case "setShowFlexOverlays":
            case "setPausedInDebuggerMessage":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Overlay.{action} is not implemented");
        }
    }
}
