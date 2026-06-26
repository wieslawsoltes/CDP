using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Interactivity;

namespace Avalonia.Diagnostics.Cdp.Domains;

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

                    var listeners = await Dispatcher.UIThread.InvokeAsync(() => GetListeners(session, target));
                    return new JsonObject { ["listeners"] = listeners };
                }

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

    private static string MapEventName(string avaloniaName)
    {
        return avaloniaName switch
        {
            "Click" => "click",
            "PointerPressed" => "mousedown",
            "PointerReleased" => "mouseup",
            "PointerMoved" => "mousemove",
            "PointerEnter" => "mouseenter",
            "PointerLeave" => "mouseleave",
            "PointerWheelChanged" => "wheel",
            "KeyDown" => "keydown",
            "KeyUp" => "keyup",
            "TextInput" => "input",
            _ => avaloniaName.ToLowerInvariant()
        };
    }

    [DynamicDependency("_eventHandlers", typeof(Interactive))]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reflection on internal Interactive._eventHandlers and RoutedEventHandlerInfo")]
    private static JsonArray GetListeners(CdpSession session, object target)
    {
        var array = new JsonArray();
        if (target is not Interactive interactive) return array;

        var type = typeof(Interactive);
        var field = type.GetField("_eventHandlers", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return array;

        var dict = field.GetValue(interactive) as IDictionary;
        if (dict == null) return array;

        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is not RoutedEvent routedEvent) continue;
            var list = entry.Value as IEnumerable;
            if (list == null) continue;

            string eventName = MapEventName(routedEvent.Name);

            foreach (var sub in list)
            {
                if (sub == null) continue;
                var subType = sub.GetType();
                var handlerProp = subType.GetProperty("Handler", BindingFlags.Public | BindingFlags.Instance);
                var routesProp = subType.GetProperty("Routes", BindingFlags.Public | BindingFlags.Instance);

                var handlerDelegate = handlerProp?.GetValue(sub) as Delegate;
                var routes = routesProp?.GetValue(sub) is RoutingStrategies r ? r : RoutingStrategies.Direct;

                bool useCapture = (routes & RoutingStrategies.Tunnel) != 0;

                string handlerName = handlerDelegate?.Method.Name ?? "Anonymous";
                string targetClassName = handlerDelegate?.Target?.GetType().Name ?? "Static";
                string displayString = $"{targetClassName}.{handlerName}";

                string objectId = handlerDelegate != null ? session.RegisterObject(handlerDelegate) : "";
                var handlerObject = new JsonObject
                {
                    ["type"] = "function",
                    ["className"] = "Function",
                    ["description"] = displayString
                };
                if (!string.IsNullOrEmpty(objectId))
                {
                    handlerObject["objectId"] = objectId;
                }

                var listener = new JsonObject
                {
                    ["type"] = eventName,
                    ["useCapture"] = useCapture,
                    ["passive"] = false,
                    ["once"] = false,
                    ["scriptId"] = "",
                    ["lineNumber"] = 0,
                    ["columnNumber"] = 0,
                    ["handler"] = handlerObject
                };

                array.Add(listener);
            }
        }

        return array;
    }
}
