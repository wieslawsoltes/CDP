using System;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class DomDebuggerDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getEventListeners":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    var target = session.GetObject(objectId);
                    if (target == null)
                    {
                        throw new Exception($"Object with ID {objectId} not found");
                    }

                    JsonArray listeners = new JsonArray();
                    if (session.Window != null)
                    {
                        listeners = await session.Window.Dispatcher.InvokeAsync(() => GetListeners(session, target));
                    }
                    return new JsonObject { ["listeners"] = listeners };
                }

            case "enable":
            case "disable":
            case "setEventListenerBreakpoint":
            case "removeEventListenerBreakpoint":
            case "removeDOMBreakpoint":
            case "removeInstrumentationBreakpoint":
            case "removeXHRBreakpoint":
            case "setBreakOnCSPViolation":
            case "setDOMBreakpoint":
            case "setInstrumentationBreakpoint":
            case "setXHRBreakpoint":
                return new JsonObject();

            default:
                throw new Exception($"Method DOMDebugger.{action} is not implemented");
        }
    }

    private static string MapEventName(string wpfName)
    {
        return wpfName switch
        {
            "Click" => "click",
            "MouseDown" => "mousedown",
            "MouseUp" => "mouseup",
            "MouseMove" => "mousemove",
            "MouseEnter" => "mouseenter",
            "MouseLeave" => "mouseleave",
            "MouseWheel" => "wheel",
            "KeyDown" => "keydown",
            "KeyUp" => "keyup",
            "TextChanged" => "input",
            _ => wpfName.ToLowerInvariant()
        };
    }

    private static JsonArray GetListeners(CdpSession session, object target)
    {
        var array = new JsonArray();
        if (target is not UIElement uiElement) return array;

        var type = target.GetType();
        var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance);

        foreach (var ev in events)
        {
            string eventName = MapEventName(ev.Name);
            var listener = new JsonObject
            {
                ["type"] = eventName,
                ["useCapture"] = false,
                ["passive"] = false,
                ["once"] = false,
                ["scriptId"] = "",
                ["lineNumber"] = 0,
                ["columnNumber"] = 0,
                ["handler"] = new JsonObject
                {
                    ["type"] = "function",
                    ["className"] = "Function",
                    ["description"] = $"{type.Name}.{ev.Name}"
                }
            };
            array.Add(listener);
        }

        return array;
    }
}
