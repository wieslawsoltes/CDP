using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Jint;

namespace Wpf.Diagnostics.Cdp.Domains;

public class CdpRuntimeDocument
{
    private readonly CdpSession _session;
    public CdpRuntimeDocument(CdpSession session) => _session = session;
    public string id => "1";
    public string nodeName => "#document";
    public CdpRuntimeWindow? defaultView => new CdpRuntimeWindow(_session);
    public CdpRuntimeElement? documentElement => _session.Window != null ? new CdpRuntimeElement(_session.Window) : null;
    public CdpRuntimeElement? body => documentElement;
    
    public CdpRuntimeElement? querySelector(string selector)
    {
        var match = SelectorEngine.QuerySelector(_session.Window ?? Application.Current.MainWindow, selector, _session.UseLogicalTree);
        return match != null ? new CdpRuntimeElement(match) : null;
    }

    public IEnumerable<CdpRuntimeElement> querySelectorAll(string selector)
    {
        var matches = SelectorEngine.QuerySelectorAll(_session.Window ?? Application.Current.MainWindow, selector, _session.UseLogicalTree);
        return matches.Select(m => new CdpRuntimeElement(m));
    }

    public CdpRuntimeElement? getElementById(string idVal)
    {
        return querySelector($"#{idVal}");
    }
}

public class CdpRuntimeWindow
{
    private readonly CdpSession _session;
    public CdpRuntimeWindow(CdpSession session) => _session = session;
    public Window? visual => _session.Window;
    public double innerWidth => visual?.ActualWidth ?? 0;
    public double innerHeight => visual?.ActualHeight ?? 0;
    public double devicePixelRatio => 1.0;
    public string title => _session.Target?.Title ?? "WPF Application";
    public CdpRuntimeDocument document => new CdpRuntimeDocument(_session);
}

public class CdpRuntimeElement
{
    public readonly Visual visual;

    public CdpRuntimeElement(Visual element)
    {
        visual = element;
    }

    public string id => visual is FrameworkElement fe ? fe.Name : "";
    public string tagName => visual.GetType().Name.ToUpperInvariant();
    public string nodeName => tagName;
    public string className => "";
    public string innerText => SelectorEngine.GetVisualTextContent(visual) ?? "";
    public string textContent => innerText;

    public double clientWidth => visual is UIElement ui ? ui.RenderSize.Width : 0;
    public double clientHeight => visual is UIElement ui ? ui.RenderSize.Height : 0;
    public double offsetWidth => clientWidth;
    public double offsetHeight => clientHeight;

    public bool isVisible => visual is UIElement ui ? ui.IsVisible : true;
    public bool isEffectivelyVisible => isVisible;

    public string getAttribute(string name)
    {
        if (name.Equals("id", StringComparison.OrdinalIgnoreCase)) return id;
        if (name.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) || name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase))
        {
            return AutomationProperties.GetAutomationId(visual);
        }
        var prop = visual.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(visual)?.ToString() ?? "";
    }

    public CdpClientRect getBoundingClientRect()
    {
        double w = clientWidth;
        double h = clientHeight;
        double x = 0;
        double y = 0;
        
        var win = Window.GetWindow(visual);
        if (win != null)
        {
            try
            {
                var pt = (visual as UIElement)?.TranslatePoint(new Point(0, 0), win);
                if (pt.HasValue)
                {
                    x = pt.Value.X;
                    y = pt.Value.Y;
                }
            }
            catch { }
        }
        return new CdpClientRect(x, y, w, h);
    }

    public bool matches(string selector)
    {
        return SelectorEngine.Matches(visual, selector);
    }
}

public class CdpClientRect
{
    public double x { get; }
    public double y { get; }
    public double width { get; }
    public double height { get; }
    public double top => y;
    public double left => x;
    public double right => x + width;
    public double bottom => y + height;

    public CdpClientRect(double x, double y, double width, double height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }
}

public class WpfScriptGlobals
{
    private readonly CdpSession _session;

    public WpfScriptGlobals(CdpSession session)
    {
        _session = session;
    }

    public Visual? SelectedNode => _session.NodeMap.GetVisual(_session.InspectedNodeId);
    public FrameworkElement? Control => SelectedNode as FrameworkElement;
    public Window? Window => (SelectedNode as Window) ?? _session.Window;
    public object? DataContext => Control?.DataContext;
    public object? ViewModel => DataContext;

    public Visual? Query(string selector)
    {
        var root = Window ?? SelectedNode;
        return root != null ? SelectorEngine.QuerySelector(root, selector, _session.UseLogicalTree) : null;
    }

    public IEnumerable<Visual> QueryAll(string selector)
    {
        var root = Window ?? SelectedNode;
        return root != null ? SelectorEngine.QuerySelectorAll(root, selector, _session.UseLogicalTree) : Enumerable.Empty<Visual>();
    }
}

public static class RuntimeDomain
{
    private static readonly ConcurrentDictionary<string, Engine> _engines = new();

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
            case "releaseObjectGroup":
            case "discardConsoleEntries":
                return new JsonObject();

            case "releaseObject":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    session.RemoteObjects.TryRemove(objectId, out _);
                    return new JsonObject();
                }

            case "evaluate":
                {
                    string expression = @params["expression"]?.GetValue<string>() ?? "";
                    bool returnByValue = @params["returnByValue"]?.GetValue<bool>() ?? false;
                    bool awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;

                    var result = await EvaluateAsync(session, expression, returnByValue);
                    return result;
                }

            case "getProperties":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    var target = session.GetObject(objectId);
                    if (target == null)
                    {
                        throw new Exception($"Object with ID {objectId} not found");
                    }

                    var dispatcher = session.Window?.Dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    var propertiesJson = await dispatcher.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
                        var props = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props)
                        {
                            try
                            {
                                if (prop.GetIndexParameters().Length > 0) continue;

                                object? val = null;
                                if (prop.CanRead)
                                {
                                    val = prop.GetValue(target);
                                }

                                var propDesc = new JsonObject
                                {
                                    ["name"] = prop.Name,
                                    ["value"] = CreateRemoteObject(session, val),
                                    ["writable"] = prop.CanWrite,
                                    ["configurable"] = true,
                                    ["enumerable"] = true
                                };
                                list.Add(propDesc);
                            }
                            catch { }
                        }
                        return list;
                    });

                    return new JsonObject { ["result"] = propertiesJson };
                }

            case "callFunctionOn":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    string functionDeclaration = @params["functionDeclaration"]?.GetValue<string>() ?? "";
                    bool returnByValue = @params["returnByValue"]?.GetValue<bool>() ?? false;
                    var arguments = @params["arguments"] as JsonArray;

                    var dispatcher = session.Window?.Dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    var result = await dispatcher.InvokeAsync(() =>
                    {
                        var target = session.GetObject(objectId);
                        if (target == null && !string.IsNullOrEmpty(objectId))
                        {
                            throw new Exception($"Object with ID {objectId} not found");
                        }

                        var engine = new Engine(options =>
                        {
                            options.Interop.TypeResolver = new Jint.Runtime.Interop.TypeResolver
                            {
                                MemberNameCreator = member => 
                                {
                                    var name = member.Name;
                                    if (string.IsNullOrEmpty(name)) return new[] { name };
                                    var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
                                    return name == camel ? new[] { name } : new[] { name, camel };
                                }
                            };
                            options.TimeoutInterval(TimeSpan.FromSeconds(5));
                        });

                        Jint.Native.JsValue thisValue;
                        if (target is Jint.Native.JsValue jsValThis)
                        {
                            thisValue = jsValThis;
                        }
                        else
                        {
                            thisValue = target != null ? Jint.Native.JsValue.FromObject(engine, target) : Jint.Native.JsValue.Null;
                        }

                        var jsArgs = new List<Jint.Native.JsValue>();
                        if (arguments != null)
                        {
                            foreach (var argNode in arguments)
                            {
                                if (argNode is JsonObject argObj)
                                {
                                    if (argObj.TryGetPropertyValue("value", out var valNode) && valNode != null)
                                    {
                                        jsArgs.Add(ConvertJsonNodeToJsValue(engine, valNode));
                                    }
                                    else if (argObj.TryGetPropertyValue("objectId", out var objIdNode) && objIdNode != null)
                                    {
                                        var argObjVal = session.GetObject(objIdNode.GetValue<string>());
                                        jsArgs.Add(Jint.Native.JsValue.FromObject(engine, argObjVal));
                                    }
                                    else
                                    {
                                        jsArgs.Add(Jint.Native.JsValue.Undefined);
                                    }
                                }
                                else
                                {
                                    jsArgs.Add(Jint.Native.JsValue.Undefined);
                                }
                            }
                        }

                        string funcCode = functionDeclaration.Trim();
                        if (funcCode.StartsWith("function") || funcCode.StartsWith("async function") || funcCode.Contains("=>"))
                        {
                            if (!funcCode.StartsWith("(") && !funcCode.EndsWith(")"))
                            {
                                funcCode = "(" + funcCode + "\n)";
                            }
                        }

                        var jsFunc = engine.Evaluate(funcCode);
                        var jsResult = engine.Invoke(jsFunc, thisValue, jsArgs.ToArray());

                        return new JsonObject
                        {
                            ["result"] = CreateRemoteObject(session, jsResult)
                        };
                    });

                    return result;
                }

            default:
                throw new Exception($"Method Runtime.{action} is not implemented");
        }
    }

    private static async Task<JsonObject> EvaluateAsync(CdpSession session, string expression, bool returnByValue)
    {
        if (expression.StartsWith("document.querySelector") || expression.StartsWith("window.") || expression.Contains("getBoundingClientRect()"))
        {
            // Evaluate Javascript via Jint
            return await EvaluateJsAsync(session, expression, returnByValue);
        }
        else
        {
            // Evaluate C# expression via Roslyn CSharpScripting
            return await EvaluateCSharpAsync(session, expression, returnByValue);
        }
    }

    private static async Task<JsonObject> EvaluateJsAsync(CdpSession session, string expression, bool returnByValue)
    {
        var dispatcher = session.Window?.Dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        return await dispatcher.InvokeAsync(() =>
        {
            try
            {
                var engine = _engines.GetOrAdd(session.Id, s =>
                {
                    var eng = new Engine();
                    eng.SetValue("window", new CdpRuntimeWindow(session));
                    eng.SetValue("document", new CdpRuntimeDocument(session));
                    return eng;
                });

                var selectedVisual = session.NodeMap.GetVisual(session.InspectedNodeId);
                engine.SetValue("$0", selectedVisual != null ? new CdpRuntimeElement(selectedVisual) : null);

                var jsResult = engine.Evaluate(expression);
                var valueNode = ConvertJsValue(jsResult);

                return new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = jsResult.IsObject() ? "object" : jsResult.IsBoolean() ? "boolean" : jsResult.IsNumber() ? "number" : "string",
                        ["value"] = valueNode
                    }
                };
            }
            catch (Exception ex)
            {
                return new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["value"] = $"Error: {ex.Message}"
                    },
                    ["exceptionDetails"] = new JsonObject
                    {
                        ["exception"] = new JsonObject
                        {
                            ["description"] = ex.ToString()
                        }
                    }
                };
            }
        });
    }

    private static JsonNode? ConvertJsValue(Jint.Native.JsValue val)
    {
        if (val.IsNull() || val.IsUndefined()) return null;
        if (val.IsBoolean()) return JsonValue.Create(val.AsBoolean());
        if (val.IsNumber()) return JsonValue.Create(val.AsNumber());
        if (val.IsString()) return JsonValue.Create(val.AsString());
        if (val.IsObject())
        {
            var obj = val.AsObject();
            if (obj is Jint.Native.Object.ObjectInstance objInst)
            {
                var dict = new JsonObject();
                foreach (var prop in objInst.GetOwnProperties())
                {
                    dict[prop.Key.ToString()] = ConvertJsValue(prop.Value.Value);
                }
                return dict;
            }
        }
        return JsonValue.Create(val.ToString());
    }

    private static async Task<JsonObject> EvaluateCSharpAsync(CdpSession session, string expression, bool returnByValue)
    {
        var dispatcher = session.Window?.Dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        return await dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var globals = new WpfScriptGlobals(session);
                var scriptOptions = ScriptOptions.Default
                    .AddImports("System", "System.Collections.Generic", "System.Linq", "System.Windows", "System.Windows.Controls", "System.Windows.Media")
                    .AddReferences(
                        typeof(Window).Assembly,
                        typeof(Visual).Assembly,
                        typeof(Button).Assembly,
                        typeof(Enumerable).Assembly
                    );

                var state = await CSharpScript.RunAsync(expression, scriptOptions, globals);
                var returnValue = state.ReturnValue;

                return new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = returnValue?.GetType().Name.ToLowerInvariant() ?? "object",
                        ["value"] = returnValue?.ToString() ?? "null"
                    }
                };
            }
            catch (Exception ex)
            {
                return new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["value"] = $"Error: {ex.Message}"
                    }
                };
            }
        }).Task.Unwrap();
    }

    private static JsonObject CreateRemoteObject(CdpSession session, object? obj)
    {
        if (obj is Jint.Native.JsValue jsVal)
        {
            if (jsVal.IsNull() || jsVal.IsUndefined())
            {
                return new JsonObject { ["type"] = "object", ["subtype"] = "null", ["value"] = null };
            }
            if (jsVal.IsBoolean())
            {
                return new JsonObject { ["type"] = "boolean", ["value"] = jsVal.AsBoolean() };
            }
            if (jsVal.IsString())
            {
                return new JsonObject { ["type"] = "string", ["value"] = jsVal.AsString() };
            }
            if (jsVal.IsNumber())
            {
                return new JsonObject { ["type"] = "number", ["value"] = jsVal.AsNumber() };
            }
            if (jsVal.IsObject())
            {
                obj = jsVal.ToObject();
            }
        }

        if (obj == null)
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["subtype"] = "null",
                ["value"] = null
            };
        }

        var type = obj.GetType();
        if (type == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean", ["value"] = (bool)obj };
        }
        if (type == typeof(string))
        {
            return new JsonObject { ["type"] = "string", ["value"] = (string)obj };
        }
        if (type == typeof(int) || type == typeof(double) || type == typeof(float) || type == typeof(long) || type == typeof(decimal))
        {
            return new JsonObject { ["type"] = "number", ["value"] = Convert.ToDouble(obj) };
        }

        string objectId = session.RegisterObject(obj);
        string className = type.FullName ?? type.Name;
        string description = obj.ToString() ?? type.Name;

        return new JsonObject
        {
            ["type"] = "object",
            ["className"] = className,
            ["description"] = description,
            ["objectId"] = objectId
        };
    }

    private static Jint.Native.JsValue ConvertJsonNodeToJsValue(Engine engine, JsonNode node)
    {
        if (node == null) return Jint.Native.JsValue.Null;
        string jsonStr = node.ToJsonString();
        var jsonParser = engine.Evaluate("JSON.parse");
        return engine.Invoke(jsonParser, jsonStr);
    }
}
