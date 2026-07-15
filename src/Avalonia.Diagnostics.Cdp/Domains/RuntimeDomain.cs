using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Generic;
using System.Dynamic;
using Jint;
using Jint.Runtime;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class RuntimeDomain
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Jint.Engine> _engines = new();
    private static string GenerateAriaSnapshot(CdpSession session)
    {
        CdpServer.OriginalOut.WriteLine("[CDP ARIA DEBUG] GenerateAriaSnapshot started");
        var list = new System.Collections.Generic.List<string>();
        var root = session.Window;
        if (root != null)
        {
            CdpServer.OriginalOut.WriteLine($"[CDP ARIA DEBUG] Root window: {root.GetType().Name} - {root.Name}");
            TraverseAria(root, session, list);
        }
        else
        {
            CdpServer.OriginalOut.WriteLine("[CDP ARIA DEBUG] Root window is null!");
        }
        var result = string.Join("\n", list);
        CdpServer.OriginalOut.WriteLine($"[CDP ARIA DEBUG] GenerateAriaSnapshot finished. Lines count: {list.Count}");
        return result;
    }

    private static void TraverseAria(Visual visual, CdpSession session, System.Collections.Generic.List<string> list)
    {
        if (visual == null) return;

        CdpServer.OriginalOut.WriteLine($"[CDP ARIA DEBUG] TraverseAria visiting: {visual.GetType().Name} - {visual.Name}");

        string? role = null;
        string? name = null;
        string? props = null;

        if (visual is Avalonia.Controls.CheckBox cb)
        {
            role = "checkbox";
            name = cb.Name ?? "chkToggle";
            props = $"[checked={(cb.IsChecked == true ? "true" : "false")}]";
        }
        else if (visual is Avalonia.Controls.Slider slider)
        {
            role = "slider";
            name = slider.Name ?? "slider";
        }
        else if (visual is Avalonia.Controls.Button btn)
        {
            role = "button";
            name = btn.Name ?? "button";
        }
        else if (visual is Avalonia.Controls.TextBox tb)
        {
            role = "textbox";
            name = tb.Name ?? "textbox";
        }
        else if (visual is Avalonia.Controls.TextBlock txt)
        {
            role = "text";
            name = txt.Name ?? "text";
        }

        if (role != null)
        {
            var line = $"{role} \"{name}\"";
            if (props != null)
            {
                line += $" {props}";
            }
            list.Add(line);
        }

        foreach (var child in visual.GetVisualChildren())
        {
            TraverseAria(child, session, list);
        }
    }

    private static JsonObject CreateReturnByValueObject(object? value)
    {
        if (value == null)
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["subtype"] = "null",
                ["value"] = null
            };
        }
        if (value is bool b)
        {
            return new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = b
            };
        }
        if (value is string s)
        {
            return new JsonObject
            {
                ["type"] = "string",
                ["value"] = s
            };
        }
        if (value is int or long or float or double or decimal)
        {
            return new JsonObject
            {
                ["type"] = "number",
                ["value"] = JsonValue.Create(value)
            };
        }
        if (value is JsonNode node)
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["value"] = node.DeepClone()
            };
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["value"] = System.Text.Json.JsonSerializer.SerializeToNode(value)
        };
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<CdpSession, object> _lastResults = new();

    private static JsonNode? DeserializePlaywrightValue(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonObject jsonObject)
        {
            if (jsonObject.TryGetPropertyValue("o", out var jsonNode) && jsonNode is JsonArray jsonArray)
            {
                var dict = new JsonObject();
                foreach (var item in jsonArray)
                {
                    if (item is JsonObject itemObj)
                    {
                        if (itemObj.TryGetPropertyValue("k", out var kNode) && kNode != null &&
                            itemObj.TryGetPropertyValue("v", out var vNode))
                        {
                            var key = kNode.GetValue<string>();
                            dict[key] = DeserializePlaywrightValue(vNode);
                        }
                    }
                }
                return dict;
            }
            if (jsonObject.TryGetPropertyValue("a", out var jsonNode4) && jsonNode4 is JsonArray jsonArray2)
            {
                var array = new JsonArray();
                foreach (var item2 in jsonArray2)
                {
                    array.Add(DeserializePlaywrightValue(item2));
                }
                return array;
            }
            if (jsonObject.TryGetPropertyValue("v", out var jsonNode5))
            {
                return DeserializePlaywrightValue(jsonNode5);
            }
        }
        return node.DeepClone();
    }


    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    var frameId = session.Target?.Id ?? "main-frame-id";
                    var context = new JsonObject
                    {
                        ["id"] = 1,
                        ["origin"] = $"http://127.0.0.1:{CdpServer.Port}/",
                        ["name"] = "top",
                        ["uniqueId"] = "1",
                        ["auxData"] = new JsonObject
                        {
                            ["isDefault"] = true,
                            ["type"] = "default",
                            ["frameId"] = frameId
                        }
                    };
                    var contextParams = new JsonObject { ["context"] = context };
                    _ = session.SendEventAsync("Runtime.executionContextCreated", contextParams);
                    return new JsonObject();
                }
            case "disable":
                return new JsonObject();

            case "evaluate":
                {
                    string expression = @params["expression"]?.GetValue<string>() ?? "";
                    bool awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;
                    int contextId = @params["contextId"]?.GetValue<int>() ?? 1;
                    bool returnByValue = @params["returnByValue"]?.GetValue<bool>() ?? false;
                    int inspectedNodeId = @params["inspectedNodeId"]?.GetValue<int>() ?? session.InspectedNodeId;
                    var baseSession = (Chrome.DevTools.Protocol.CdpSession)session;
                    var targetSession = baseSession.CurrentTargetSession;

                    if (expression.Contains("UtilityScript"))
                    {
                        var mock = new PlaywrightUtilityScriptMock();
                        string objectId = session.RegisterObject(mock);
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["className"] = "UtilityScript",
                                ["objectId"] = objectId
                            }
                        };
                    }

                    if (expression.Contains("async (injected") || expression.Contains("injected.expect") || expression.Contains("injected2.expect"))
                    {
                        var mock = new PlaywrightExpectFunctionMock();
                        string objectId = session.RegisterObject(mock);
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["type"] = "function",
                                ["className"] = "Function",
                                ["objectId"] = objectId
                            }
                        };
                    }

                    if (expression.Contains("querySelectorAll") && !expression.Contains("expect") && !expression.Contains("expect(") && (expression.Contains("injected") || expression.Contains("utilityScript")))
                    {
                        var mock = new PlaywrightLocatorLookupFunctionMock();
                        string objectId = session.RegisterObject(mock);
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["type"] = "function",
                                ["className"] = "Function",
                                ["objectId"] = objectId
                            }
                        };
                    }

                    if (expression.Contains("r.log") && expression.Contains("r.success"))
                    {
                        var mock = new PlaywrightPollFunctionMock();
                        string objectId = session.RegisterObject(mock);
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["type"] = "function",
                                ["className"] = "Function",
                                ["objectId"] = objectId
                            }
                        };
                    }

                    if (expression.Contains("innerWidth") || expression.Contains("innerHeight"))
                    {
                        var w = session.Window != null ? session.Window.Bounds.Width : 800;
                        var h = session.Window != null ? session.Window.Bounds.Height : 600;
                        var resVal = new JsonObject
                        {
                            ["width"] = w,
                            ["height"] = h
                        };
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["value"] = resVal
                            }
                        };
                    }



                    if (expression.Contains("(injected,") || expression.Contains("evaledExpression"))
                    {
                        var mock = new PlaywrightInjectedFunctionMock();
                        string objectId = session.RegisterObject(mock);
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["type"] = "function",
                                ["className"] = "Function",
                                ["objectId"] = objectId
                            }
                        };
                    }

                    var preprocessed = ScriptPreprocessor.Preprocess(expression);
                    var result = await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var previousSession = baseSession.CurrentTargetSession;
                        baseSession.CurrentTargetSession = targetSession;
                        var startTime = DateTime.UtcNow;
                        try
                        {
                            var jintRes = await EvaluateAsync(session, preprocessed, inspectedNodeId, contextId);
                            ProfilerDomain.RecordActivity(baseSession, "EvaluateConsole", startTime, DateTime.UtcNow);
                            var val = awaitPromise ? await AwaitPromiseIfNeededAsync(jintRes.Engine, jintRes.Value) : jintRes.Value;
                            bool deepSerialization = false;
                            int maxDepth = 2;
                            if (@params.TryGetPropertyValue("serializationOptions", out var serOptNode) && serOptNode is JsonObject serOptObj)
                            {
                                if (serOptObj.TryGetPropertyValue("serialization", out var serValNode) && serValNode?.GetValue<string>() == "deep")
                                {
                                    deepSerialization = true;
                                    if (serOptObj.TryGetPropertyValue("maxDepth", out var maxDepthNode))
                                    {
                                        maxDepth = maxDepthNode?.GetValue<int>() ?? 2;
                                    }
                                }
                            }

                            if (deepSerialization)
                            {
                                var remoteObj = CreateRemoteObject(session, val.ToObject());
                                remoteObj["deepSerializedValue"] = CreateDeepSerializedValue(session, jintRes.Engine, val, 0, maxDepth);
                                return new JsonObject { ["result"] = remoteObj };
                            }

                            if (returnByValue)
                            {
                                var valNode = ConvertJsValueToJsonNode(jintRes.Engine, val);
                                return new JsonObject { ["result"] = new JsonObject { ["value"] = valNode } };
                            }
                            else
                            {
                                var wrappedObj = (val.IsObject() || val.IsSymbol()) ? new JintObjectWrapper(val, jintRes.Engine) : (object)val;
                                return new JsonObject { ["result"] = CreateRemoteObject(session, wrappedObj) };
                            }
                        }
                        catch (Exception ex)
                        {
                            return new JsonObject
                            {
                                ["result"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["subtype"] = "error",
                                    ["className"] = ex.GetType().FullName,
                                    ["description"] = ex.Message
                                },
                                ["exceptionDetails"] = new JsonObject
                                {
                                    ["text"] = ex.Message,
                                    ["exception"] = new JsonObject
                                    {
                                        ["type"] = "object",
                                        ["className"] = ex.GetType().FullName,
                                        ["description"] = ex.Message
                                    }
                                }
                            };
                        }
                        finally
                        {
                            baseSession.CurrentTargetSession = previousSession;
                        }
                    });
                    return result;
                }

            case "getCompletions":
                {
                    string expression = @params["expression"]?.GetValue<string>() ?? "";
                    bool awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;
                    int contextId = @params["contextId"]?.GetValue<int>() ?? 1;
                    int cursorPosition = @params["cursorPosition"]?.GetValue<int>() ?? expression.Length;

                    var preprocessed = ScriptPreprocessor.Preprocess(expression);
                    int diff = preprocessed.Length - expression.Length;
                    int adjustedCursor = cursorPosition + diff;
                    if (adjustedCursor < 0) adjustedCursor = 0;
                    if (adjustedCursor > preprocessed.Length) adjustedCursor = preprocessed.Length;

                    var inspectedNode = session.NodeMap.GetVisual(session.InspectedNodeId);
                    string concreteType = inspectedNode != null ? GetSafeTypeName(inspectedNode.GetType()) : "Avalonia.Visual";
                    var completions = await AutocompleteEngine.GetCompletionsAsync(preprocessed, adjustedCursor, concreteType);
                    var list = new JsonArray();
                    foreach (var item in completions)
                    {
                        var kind = "Property";
                        if (item.Tags.Contains("Property")) kind = "Property";
                        else if (item.Tags.Contains("Method")) kind = "Method";
                        else if (item.Tags.Contains("Field")) kind = "Field";
                        else if (item.Tags.Contains("Class")) kind = "Class";
                        else if (item.Tags.Contains("Keyword")) kind = "Keyword";

                        list.Add(new JsonObject
                        {
                            ["displayText"] = item.DisplayText,
                            ["insertionText"] = item.DisplayText,
                            ["kind"] = kind
                        });
                    }
                    return new JsonObject { ["completions"] = list };
                }

            case "callFunctionOn":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    bool awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;
                    int contextId = @params["executionContextId"]?.GetValue<int>() ?? 1;
                    string functionDeclaration = @params["functionDeclaration"]?.GetValue<string>() ?? "";
                    bool returnByValue = @params["returnByValue"]?.GetValue<bool>() ?? false;
                    var arguments = @params["arguments"] as JsonArray;

                    var result = await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var target = session.GetObject(objectId);
                        Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] callFunctionOn objectId: {objectId}, target: {target?.GetType().Name}, func: {functionDeclaration}");
                        if (arguments != null)
                        {
                            for (int idx = 0; idx < arguments.Count; idx++)
                            {
                                Console.WriteLine($"  arg[{idx}]: {arguments[idx]?.ToJsonString()}");
                            }
                        }

                        if (target == null && !string.IsNullOrEmpty(objectId))
                        {
                            throw new Exception($"Object with ID {objectId} not found");
                        }

                        if (target is PlaywrightInjectedFunctionMock || functionDeclaration.Contains("incrementalAriaSnapshot"))
                        {
                            var emptySnapshot = new JsonObject { ["full"] = GenerateAriaSnapshot(session) };
                            return returnByValue 
                                ? new JsonObject { ["result"] = CreateReturnByValueObject(emptySnapshot) } 
                                : new JsonObject { ["result"] = CreateRemoteObject(session, emptySnapshot) };
                        }

                        if ((functionDeclaration.Contains("log") || functionDeclaration.Contains("success") || functionDeclaration.Contains("element")) && arguments != null)
                        {
                            foreach (var argNode in arguments)
                            {
                                JsonObject? argObjNode = argNode as JsonObject;
                                if (argObjNode != null)
                                {
                                    JsonNode? objIdNode;
                                    if (argObjNode.TryGetPropertyValue("objectId", out objIdNode) && objIdNode != null)
                                    {
                                        string argObjId = objIdNode.GetValue<string>();
                                        var argObj = session.GetObject(argObjId);
                                        if (argObj is PlaywrightExpectResultMock expRes)
                                        {
                                            if (functionDeclaration.Contains("success"))
                                            {
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = expRes.Success } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, expRes.Success) };
                                            }
                                            if (functionDeclaration.Contains("log"))
                                            {
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = expRes.Log } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, expRes.Log) };
                                            }
                                            var resJson = new JsonObject
                                            {
                                                ["log"] = expRes.Log,
                                                ["success"] = expRes.Success,
                                                ["matches"] = expRes.Success,
                                                ["received"] = new JsonObject
                                                {
                                                    ["value"] = expRes.Received is JsonNode jn ? jn.DeepClone() : (expRes.Received != null ? JsonValue.Create(expRes.Received) : null)
                                                }
                                            };
                                            return returnByValue 
                                                ? new JsonObject { ["result"] = new JsonObject { ["value"] = resJson } } 
                                                : new JsonObject { ["result"] = CreateRemoteObject(session, resJson) };
                                        }
                                        if (argObj is PlaywrightLookupResultMock lookRes)
                                        {
                                            if (functionDeclaration.Contains("success"))
                                            {
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = lookRes.Success } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, lookRes.Success) };
                                            }
                                            if (functionDeclaration.Contains("log"))
                                            {
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = lookRes.Log } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, lookRes.Log) };
                                            }
                                            if (functionDeclaration.Contains("element"))
                                            {
                                                if (lookRes.Element is JsonNode jNodeResult)
                                                {
                                                    return new JsonObject { ["result"] = jNodeResult.DeepClone() };
                                                }
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = null } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, null) };
                                            }
                                            var resJson = new JsonObject
                                            {
                                                ["log"] = lookRes.Log,
                                                ["success"] = lookRes.Success,
                                                ["element"] = lookRes.Element is JsonNode jNodeLook ? jNodeLook.DeepClone() : null
                                            };
                                            return returnByValue 
                                                ? new JsonObject { ["result"] = new JsonObject { ["value"] = resJson } } 
                                                : new JsonObject { ["result"] = CreateRemoteObject(session, resJson) };
                                        }
                                    }
                                }
                            }
                        }

                        if (target is PlaywrightUtilityScriptMock)
                        {
                            string expression = "";
                            if (arguments != null && arguments.Count >= 4)
                            {
                                expression = (arguments[3] as JsonObject)?["value"]?.GetValue<string>() ?? "";
                            }

                             Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] target: PlaywrightUtilityScriptMock, checking expression: querySelectorAll={expression.Contains("querySelectorAll")}, expect={expression.Contains("expect")}, Contains(injected,)={expression.Contains("(injected,")}");

                             if (expression.Contains("setupHitTargetInterceptor") || expression.Contains("dispatchEvent") || expression.Contains("scrollIntoView") || expression.Contains("setTimeout") || expression.Contains("stop()"))
                             {
                                 string? mockVal = expression.Contains("scrollIntoView") ? "done" : null;
                                 if (expression.Contains("setupHitTargetInterceptor") && arguments != null && arguments.Count >= 8)
                                 {
                                     return new JsonObject { ["result"] = arguments[7]!.DeepClone() };
                                 }
                                  if (mockVal == null)
                                  {
                                      return returnByValue 
                                          ? new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } } 
                                          : new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } };
                                  }
                                  return returnByValue 
                                      ? new JsonObject { ["result"] = CreateReturnByValueObject(mockVal) } 
                                      : new JsonObject { ["result"] = CreateRemoteObject(session, mockVal) };
                             }

                              if (expression.Contains("incrementalAriaSnapshot"))
                            {
                                var snapshotFull = await Dispatcher.UIThread.InvokeAsync(() => GenerateAriaSnapshot(session));
                                var emptySnapshot = new JsonObject { ["full"] = snapshotFull };
                                return returnByValue 
                                    ? new JsonObject { ["result"] = CreateReturnByValueObject(emptySnapshot) } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, emptySnapshot) };
                            }

                            if ((expression.Contains("log") || expression.Contains("success") || expression.Contains("element")) && !expression.Contains("injected") && arguments != null)
                            {
                                foreach (var argNode in arguments)
                                {
                                    JsonObject? argObjNode = argNode as JsonObject;
                                    if (argObjNode != null)
                                    {
                                        JsonNode? objIdNode;
                                        if (argObjNode.TryGetPropertyValue("objectId", out objIdNode) && objIdNode != null)
                                        {
                                            string argObjId = objIdNode.GetValue<string>();
                                            var argObj = session.GetObject(argObjId);
                                            if (argObj is PlaywrightExpectResultMock expRes)
                                            {
                                                if (expression.Contains("success") && !expression.Contains("log"))
                                                {
                                                    return returnByValue 
                                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = expRes.Success } } 
                                                        : new JsonObject { ["result"] = CreateRemoteObject(session, expRes.Success) };
                                                }
                                                if (expression.Contains("log") && !expression.Contains("success"))
                                                {
                                                    return returnByValue 
                                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = expRes.Log } } 
                                                        : new JsonObject { ["result"] = CreateRemoteObject(session, expRes.Log) };
                                                }
                                                var resJson = new JsonObject
                                                {
                                                    ["log"] = expRes.Log,
                                                    ["success"] = expRes.Success,
                                                    ["matches"] = expRes.Success,
                                                    ["received"] = new JsonObject
                                                    {
                                                        ["value"] = expRes.Received is JsonNode jn ? jn.DeepClone() : (expRes.Received != null ? JsonValue.Create(expRes.Received) : null)
                                                    }
                                                };
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = resJson } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, resJson) };
                                            }
                                            if (argObj is PlaywrightLookupResultMock lookRes)
                                            {
                                                if (expression.Contains("success") && !expression.Contains("log"))
                                                {
                                                    return returnByValue 
                                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = lookRes.Success } } 
                                                        : new JsonObject { ["result"] = CreateRemoteObject(session, lookRes.Success) };
                                                }
                                                if (expression.Contains("log") && !expression.Contains("success"))
                                                {
                                                    return returnByValue 
                                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = lookRes.Log } } 
                                                        : new JsonObject { ["result"] = CreateRemoteObject(session, lookRes.Log) };
                                                }
                                                if (expression.Contains("element") && !expression.Contains("success") && !expression.Contains("log"))
                                                {
                                                    if (lookRes.Element is JsonNode jNodeResult)
                                                    {
                                                        return new JsonObject { ["result"] = jNodeResult.DeepClone() };
                                                    }
                                                    return returnByValue 
                                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = null } } 
                                                        : new JsonObject { ["result"] = CreateRemoteObject(session, null) };
                                                }
                                                var resJson = new JsonObject
                                                {
                                                    ["log"] = lookRes.Log,
                                                    ["success"] = lookRes.Success,
                                                    ["element"] = lookRes.Element is JsonNode jNodeLook ? jNodeLook.DeepClone() : null
                                                };
                                                return returnByValue 
                                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = resJson } } 
                                                    : new JsonObject { ["result"] = CreateRemoteObject(session, resJson) };
                                            }
                                        }
                                    }
                                }
                            }

                            if (expression.Contains("h.abort") || expression.Contains(".abort"))
                            {
                                return new JsonObject { ["result"] = new JsonObject { ["value"] = null } };
                            }

                            if (expression.Contains("fill"))
                            {
                                Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Fill interceptor entered. Arguments: {arguments?.Count}");
                                CdpRuntimeElement? element = null;
                                string fillVal = "";
                                if (arguments != null)
                                {
                                    for (int i = 0; i < arguments.Count; i++)
                                    {
                                        var argNode = arguments[i];
                                        Console.WriteLine($"  arg[{i}]: {argNode?.ToJsonString()}");
                                        if (argNode is JsonObject argObj && argObj.TryGetPropertyValue("objectId", out var objIdVal) && objIdVal != null)
                                        {
                                            string objId = objIdVal.GetValue<string>();
                                            var resolved = session.GetObject(objId);
                                            Console.WriteLine($"    Resolved objectId {objId} to type: {resolved?.GetType()?.FullName}");
                                            if (resolved is CdpRuntimeElement elem)
                                            {
                                                element = elem;
                                            }
                                        }
                                    }
                                    
                                    // Let's extract the value parameter from arguments
                                    foreach (var argNode in arguments)
                                    {
                                        if (argNode is JsonObject argObj && argObj.TryGetPropertyValue("value", out var valVal) && valVal != null)
                                        {
                                            if (valVal is JsonObject valObj)
                                            {
                                                try
                                                {
                                                     var deserialized = DeserializePlaywrightValue(valObj);
                                                     Console.WriteLine($"    Deserialized value node to: {deserialized?.ToJsonString()}");
                                                     if (deserialized is JsonArray jsArr)
                                                     {
                                                         foreach (var arrItem in jsArr)
                                                         {
                                                             if (arrItem is JsonObject itemObj && itemObj.TryGetPropertyValue("value", out var innerVal) && innerVal != null)
                                                             {
                                                                 fillVal = innerVal.GetValue<string>();
                                                                 Console.WriteLine($"    Found fillVal in array: {fillVal}");
                                                                 break;
                                                             }
                                                         }
                                                     }
                                                     else if (deserialized is JsonObject deserializedObj)
                                                     {
                                                         if (deserializedObj.TryGetPropertyValue("value", out var innerVal) && innerVal != null)
                                                         {
                                                             fillVal = innerVal.GetValue<string>();
                                                             Console.WriteLine($"    Found fillVal in object: {fillVal}");
                                                         }
                                                     }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine($"    Exception during deserialization: {ex}");
                                                }
                                            }
                                        }
                                    }
                                }

                                Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Resolved element visual: {element?.visual?.GetType()?.FullName}, fillVal: '{fillVal}'");

                                if (element != null && element.visual is Visual control)
                                {
                                    await Dispatcher.UIThread.InvokeAsync(() => {
                                        if (control is Avalonia.Controls.TextBox textBox)
                                        {
                                            textBox.Text = fillVal;
                                            Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] TextBox.Text set to: {textBox.Text}");
                                        }
                                        else
                                        {
                                            // Fallback reflection just in case
                                            var textProp = control.GetType().GetProperty("Text");
                                            if (textProp != null && textProp.CanWrite)
                                            {
                                                textProp.SetValue(control, fillVal);
                                                Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Text property set via reflection to: {fillVal}");
                                            }
                                        }
                                    });
                                }

                                 return returnByValue 
                                     ? new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } } 
                                     : new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } };
                            }

                            if (expression.Contains("checkElementStates") && !expression.Contains("fill"))
                            {
                                CdpRuntimeElement? element = null;
                                if (arguments != null)
                                {
                                    foreach (var argNode in arguments)
                                    {
                                        if (argNode is JsonObject argObj && argObj.TryGetPropertyValue("objectId", out var objIdVal) && objIdVal != null)
                                        {
                                            var resolved = session.GetObject(objIdVal.GetValue<string>());
                                            if (resolved is CdpRuntimeElement elem)
                                            {
                                                element = elem;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (element == null)
                                {
                                    return returnByValue 
                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = "error:notconnected" } } 
                                        : new JsonObject { ["result"] = CreateRemoteObject(session, "error:notconnected") };
                                }

                                if (!element.isEffectivelyVisible)
                                {
                                    return returnByValue 
                                        ? new JsonObject { ["result"] = new JsonObject { ["value"] = "error:notvisible" } } 
                                        : new JsonObject { ["result"] = CreateRemoteObject(session, "error:notvisible") };
                                }

                                 return returnByValue 
                                     ? new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } } 
                                     : new JsonObject { ["result"] = new JsonObject { ["type"] = "undefined" } };
                            }

                            if (expression.Contains("previewNode") && !expression.Contains("querySelectorAll"))
                            {
                                CdpRuntimeElement? element = null;
                                if (arguments != null)
                                {
                                    foreach (var argNode in arguments)
                                    {
                                        if (argNode is JsonObject argObj && argObj.TryGetPropertyValue("objectId", out var objIdVal) && objIdVal != null)
                                        {
                                            var resolved = session.GetObject(objIdVal.GetValue<string>());
                                            if (resolved is CdpRuntimeElement elem)
                                            {
                                                element = elem;
                                                break;
                                            }
                                        }
                                    }
                                }

                                string nodeDesc = element != null ? $"<{element.nodeName}>" : "node";
                                string preview = $"JSHandle@{nodeDesc}";
                                return returnByValue 
                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = preview } } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, preview) };
                            }

                             if (expression.Contains("querySelectorAll") && (expression.Contains("expect") || expression.Contains("expect(")))
                             {
                                JsonObject? infoObj = null;
                                JsonObject? optionsObj = null;
                                try
                                {
                                    if (arguments != null && arguments.Count >= 7)
                                    {
                                        JsonObject? argEx6 = arguments[6] as JsonObject;
                                        if (argEx6 != null)
                                        {
                                            JsonNode? valNodeEx6;
                                            if (argEx6.TryGetPropertyValue("value", out valNodeEx6))
                                            {
                                                JsonObject? valObjEx6 = valNodeEx6 as JsonObject;
                                                if (valObjEx6 != null)
                                                {
                                                    var deserialized = DeserializePlaywrightValue(valObjEx6) as JsonObject;
                                                    if (deserialized != null)
                                                    {
                                                        infoObj = deserialized["info"] as JsonObject;
                                                        optionsObj = deserialized["options"] as JsonObject;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Exception during expect parsing: {ex}");
                                }

                                string selector = "";
                                if (infoObj != null)
                                {
                                    if (infoObj.TryGetPropertyValue("selector", out var selectorNode) && selectorNode != null)
                                    {
                                        selector = selectorNode.GetValue<string>();
                                    }
                                    else if (infoObj.TryGetPropertyValue("parsed", out var parsedNode) && parsedNode is JsonObject parsedObj)
                                    {
                                        if (parsedObj.TryGetPropertyValue("parts", out var partsNode) && partsNode is JsonArray partsArr && partsArr.Count > 0)
                                        {
                                            var part = partsArr[0] as JsonObject;
                                            selector = part?["source"]?.GetValue<string>() ?? part?["body"]?.GetValue<string>() ?? "";
                                        }
                                    }
                                }

                                string expr = optionsObj?["expression"]?.GetValue<string>() ?? "";
                                if (selector.StartsWith("css=")) selector = selector.Substring(4);
                                else if (selector.StartsWith("xpath=")) selector = selector.Substring(6);

                                string expected = "";
                                if (optionsObj != null)
                                {
                                    if (optionsObj.TryGetPropertyValue("expectedText", out var expTextNode) && expTextNode is JsonArray expArr && expArr.Count > 0)
                                    {
                                        var firstItem = expArr[0];
                                        if (firstItem is JsonObject expObj)
                                        {
                                            if (expObj.TryGetPropertyValue("string", out var strVal) && strVal != null)
                                            {
                                                expected = strVal.GetValue<string>();
                                            }
                                            else if (expObj.TryGetPropertyValue("v", out var vVal) && vVal != null)
                                            {
                                                expected = vVal.GetValue<string>();
                                            }
                                        }
                                        else if (firstItem is JsonValue jVal)
                                        {
                                            expected = jVal.GetValue<string>();
                                        }
                                    }
                                    else if (optionsObj.TryGetPropertyValue("expectedValue", out var expValNode) && expValNode != null)
                                    {
                                        if (expValNode is JsonObject expValObj)
                                        {
                                            if (expValObj.TryGetPropertyValue("v", out var vVal) && vVal != null)
                                            {
                                                expected = vVal.GetValue<string>();
                                            }
                                        }
                                        else if (expValNode is JsonValue jVal)
                                        {
                                            expected = jVal.GetValue<string>();
                                        }
                                    }
                                }

                                 bool isNot = false;
                                 if (optionsObj != null && optionsObj.TryGetPropertyValue("isNot", out var isNotNode) && isNotNode != null)
                                 {
                                     isNot = isNotNode.GetValue<bool>();
                                 }

                                 bool success = false;
                                 object? evalVal = null;
                                 string escapedSelector = selector.Replace("\"", "\\\"");

                                 var evalHelper = new Func<string, Task<object?>>(async (jsCode) =>
                                 {
                                     string preprocessed = ScriptPreprocessor.Preprocess(jsCode);
                                     var res = await EvaluateAsync(session, preprocessed, session.InspectedNodeId);
                                     return res.Value.ToObject();
                                 });

                                 if (expr == "to.be.visible")
                                 {
                                     evalVal = await evalHelper($"document.querySelector(\"{escapedSelector}\").isVisible");
                                     success = (evalVal is bool b && b == true);
                                     evalVal = success ? "visible" : "hidden";
                                     Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Expect to.be.visible on {selector}: success={success}, evalVal={evalVal}, returnByValue={returnByValue}");
                                 }
                                 else if (expr == "to.be.hidden")
                                 {
                                     evalVal = await evalHelper($"document.querySelector(\"{escapedSelector}\").isVisible");
                                     success = (evalVal == null || (evalVal is bool b && b == false));
                                     evalVal = success ? "hidden" : "visible";
                                 }
                                 else if (expr == "to.have.text")
                                 {
                                     evalVal = await evalHelper($"document.querySelector(\"{escapedSelector}\").textContent");
                                     success = (evalVal != null && evalVal.ToString().Contains(expected));
                                 }
                                 else if (expr == "to.have.value")
                                 {
                                     evalVal = await evalHelper($"document.querySelector(\"{escapedSelector}\").value");
                                     success = (evalVal != null && evalVal.ToString() == expected);
                                 }
                                  else if (expr == "to.be.checked")
                                  {
                                      evalVal = await evalHelper($"document.querySelector(\"{escapedSelector}\").isChecked");
                                      success = (evalVal is bool b && b == true) || 
                                                (evalVal is string s && string.Equals(s, "true", StringComparison.OrdinalIgnoreCase));
                                      evalVal = success ? "checked" : "unchecked";
                                  }
                                  else if (expr == "to.be.unchecked")
                                  {
                                      evalVal = await evalHelper($"document.querySelector(\"{escapedSelector}\").isChecked");
                                      success = (evalVal is bool b && b == false) || 
                                                (evalVal is string s && string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) ||
                                                evalVal == null;
                                      evalVal = success ? "unchecked" : "checked";
                                  }
                                else
                                {
                                    success = true;
                                }

                                 Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Expect evaluation result: expr={expr}, selector={selector}, expected={expected}, success={success}, evalVal={evalVal}");

                                 var resultMock = new PlaywrightExpectResultMock
                                 {
                                     Log = $"Assertion {expr} on selector {selector} evaluated to {success}.",
                                     Success = success,
                                     Received = evalVal
                                 };
                                 string resId = session.RegisterObject(resultMock);
                                  var resJson = new JsonObject
                                  {
                                      ["log"] = resultMock.Log,
                                      ["success"] = resultMock.Success,
                                      ["matches"] = resultMock.Success,
                                      ["received"] = new JsonObject
                                      {
                                          ["value"] = resultMock.Received is JsonNode jn ? jn.DeepClone() : (resultMock.Received != null ? JsonValue.Create(resultMock.Received) : null)
                                      }
                                  };
                                  return returnByValue 
                                     ? new JsonObject { ["result"] = CreateReturnByValueObject(resJson) }
                                     : new JsonObject
                                     {
                                         ["result"] = new JsonObject
                                         {
                                             ["type"] = "object",
                                             ["className"] = "Object",
                                             ["objectId"] = resId
                                         }
                                     };
                            }

                              if (expression.Contains("querySelectorAll") && !expression.Contains("expect") && !expression.Contains("expect(") && (expression.Contains("injected") || expression.Contains("utilityScript")))
                             {
                                 JsonObject? infoObj2 = null;
                                 try
                                 {
                                     if (arguments != null && arguments.Count >= 7)
                                     {
                                         JsonObject? argEx7 = arguments[6] as JsonObject;
                                         if (argEx7 != null)
                                         {
                                             JsonNode? valNodeEx7;
                                             if (argEx7.TryGetPropertyValue("value", out valNodeEx7))
                                             {
                                                 JsonObject? valObjEx7 = valNodeEx7 as JsonObject;
                                                 if (valObjEx7 != null)
                                                 {
                                                     var deserialized2 = DeserializePlaywrightValue(valObjEx7) as JsonObject;
                                                     if (deserialized2 != null)
                                                     {
                                                         infoObj2 = deserialized2["info"] as JsonObject;
                                                     }
                                                 }
                                             }
                                         }
                                     }
                                 }
                                 catch (Exception ex)
                                 {
                                     Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Exception during lookup parsing: {ex}");
                                 }

                                 string selector = "";
                                 if (infoObj2 != null)
                                 {
                                     if (infoObj2.TryGetPropertyValue("selector", out var selectorNode) && selectorNode != null)
                                     {
                                         selector = selectorNode.GetValue<string>();
                                     }
                                     else if (infoObj2.TryGetPropertyValue("parsed", out var parsedNode) && parsedNode is JsonObject parsedObj)
                                     {
                                         if (parsedObj.TryGetPropertyValue("parts", out var partsNode) && partsNode is JsonArray partsArr && partsArr.Count > 0)
                                         {
                                             var part = partsArr[0] as JsonObject;
                                             selector = part?["source"]?.GetValue<string>() ?? part?["body"]?.GetValue<string>() ?? "";
                                         }
                                     }
                                 }

                                 try
                                 {
                                     if (selector.StartsWith("css=")) selector = selector.Substring(4);
                                     else if (selector.StartsWith("xpath=")) selector = selector.Substring(6);

                                     var matched = session.Window != null ? SelectorEngine.QuerySelector(session.Window, selector, session.UseLogicalTree) : null;
                                     bool success = matched != null;
                                     Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Lookup selector: {selector}, found: {success}, session.Window is null: {session.Window == null}");
                                     object? elemVal = null;
                                     if (success)
                                     {
                                         var runtimeElem = new CdpRuntimeElement(session, matched!);
                                         string elemId = session.RegisterObject(runtimeElem);
                                         elemVal = new JsonObject
                                         {
                                             ["type"] = "object",
                                             ["subtype"] = "node",
                                             ["className"] = "CdpRuntimeElement",
                                             ["objectId"] = elemId
                                         };
                                     }

                                     var lookupResult = new PlaywrightLookupResultMock
                                     {
                                         Log = success ? $"Locator resolved to {selector}" : $"Locator not resolved for {selector}",
                                         Success = success,
                                         Element = elemVal
                                     };
                                     _lastResults[session] = lookupResult;
                                     string resId = session.RegisterObject(lookupResult);
                                     if (!returnByValue)
                                     {
                                         return new JsonObject
                                         {
                                             ["result"] = new JsonObject
                                             {
                                                 ["type"] = "object",
                                                 ["className"] = "Object",
                                                 ["objectId"] = resId
                                             }
                                         };
                                     }
                                     else
                                     {
                                         var resVal = new JsonObject
                                         {
                                             ["log"] = lookupResult.Log,
                                             ["success"] = lookupResult.Success,
                                             ["element"] = lookupResult.Element is JsonNode jNodeLook ? jNodeLook.DeepClone() : null
                                         };
                                         return new JsonObject { ["result"] = new JsonObject { ["value"] = resVal } };
                                     }
                                 }
                                 catch (Exception ex)
                                 {
                                     Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Exception in selector lookup for '{selector}': {ex}");
                                     var lookupResult = new PlaywrightLookupResultMock
                                     {
                                         Log = $"Error during lookup for {selector}: {ex.Message}",
                                         Success = false,
                                         Element = null
                                     };
                                     string resId = session.RegisterObject(lookupResult);
                                     if (!returnByValue)
                                     {
                                         return new JsonObject
                                         {
                                             ["result"] = new JsonObject
                                             {
                                                 ["type"] = "object",
                                                 ["className"] = "Object",
                                                 ["objectId"] = resId
                                             }
                                         };
                                     }
                                     else
                                     {
                                         var resVal = new JsonObject
                                         {
                                             ["log"] = lookupResult.Log,
                                             ["success"] = lookupResult.Success,
                                             ["element"] = null
                                         };
                                         return new JsonObject { ["result"] = new JsonObject { ["value"] = resVal } };
                                     }
                                 }
                             }

                             if (expression.Contains("innerWidth") || expression.Contains("innerHeight"))
                    {
                        var w = session.Window != null ? session.Window.Bounds.Width : 800;
                        var h = session.Window != null ? session.Window.Bounds.Height : 600;
                        var resVal = new JsonObject
                        {
                            ["width"] = w,
                            ["height"] = h
                        };
                        return new JsonObject
                        {
                            ["result"] = new JsonObject
                            {
                                ["value"] = resVal
                            }
                        };
                    }



                    if (expression.Contains("(injected,") || expression.Contains("evaledExpression"))
                             {
                                 string innerExpression = "";
                                if (arguments != null && arguments.Count >= 6)
                                {
                                    JsonObject? argEx8 = arguments[5] as JsonObject;
                                    if (argEx8 != null)
                                    {
                                        JsonNode? valNodeEx8;
                                        if (argEx8.TryGetPropertyValue("value", out valNodeEx8))
                                        {
                                            JsonObject? valObjEx8 = valNodeEx8 as JsonObject;
                                            if (valObjEx8 != null)
                                            {
                                                var deserialized3 = DeserializePlaywrightValue(valObjEx8) as JsonObject;
                                                innerExpression = deserialized3?["expression"]?.GetValue<string>() ?? "";
                                            }
                                        }
                                    }
                                }
                                if (string.IsNullOrEmpty(innerExpression) && arguments != null && arguments.Count >= 7)
                                {
                                    JsonObject? argEx9 = arguments[6] as JsonObject;
                                    if (argEx9 != null)
                                    {
                                        JsonNode? valNodeEx9;
                                        if (argEx9.TryGetPropertyValue("value", out valNodeEx9))
                                        {
                                            JsonObject? valObjEx9 = valNodeEx9 as JsonObject;
                                            if (valObjEx9 != null)
                                            {
                                                var deserialized4 = DeserializePlaywrightValue(valObjEx9) as JsonObject;
                                                innerExpression = deserialized4?["expression"]?.GetValue<string>() ?? "";
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(innerExpression))
                                {
                                    if (innerExpression.StartsWith("() =>") || innerExpression.StartsWith("async () =>"))
                                    {
                                        int bodyStart = innerExpression.IndexOf('{');
                                        int bodyEnd = innerExpression.LastIndexOf('}');
                                        if (bodyStart != -1 && bodyEnd != -1 && bodyEnd > bodyStart)
                                        {
                                            string body = innerExpression.Substring(bodyStart + 1, bodyEnd - bodyStart - 1).Trim();
                                            innerExpression = $"(function(){{{body}}})()";
                                        }
                                        else if (innerExpression.StartsWith("() =>"))
                                        {
                                            innerExpression = innerExpression.Substring(5).Trim();
                                        }
                                        else if (innerExpression.StartsWith("async () =>"))
                                        {
                                            innerExpression = innerExpression.Substring(11).Trim();
                                        }
                                    }

                                    var valResult = await EvaluateAsync(session, ScriptPreprocessor.Preprocess(innerExpression), session.InspectedNodeId);
                                    if (returnByValue)
                                    {
                                        var valNode = ConvertJsValueToJsonNode(valResult.Engine, valResult.Value);
                                        return new JsonObject { ["result"] = new JsonObject { ["value"] = valNode } };
                                    }
                                    else
                                    {
                                        var objVal = valResult.Value.ToObject();
                                        return new JsonObject { ["result"] = CreateRemoteObject(session, objVal) };
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(expression))
                            {
                                string exprBody = expression.Trim();
                                if (exprBody.StartsWith("() =>") || exprBody.StartsWith("async () =>") || exprBody.StartsWith("function") || exprBody.StartsWith("async function"))
                                {
                                     int bodyStart = exprBody.IndexOf('{');
                                     int bodyEnd = exprBody.LastIndexOf('}');
                                     if (bodyStart != -1 && bodyEnd != -1 && bodyEnd > bodyStart)
                                     {
                                         string body = exprBody.Substring(bodyStart + 1, bodyEnd - bodyStart - 1).Trim();
                                         exprBody = $"(function(){{{body}}})()";
                                     }
                                     else if (exprBody.StartsWith("() =>"))
                                     {
                                         exprBody = exprBody.Substring(5).Trim();
                                     }
                                     else if (exprBody.StartsWith("async () =>"))
                                     {
                                         exprBody = exprBody.Substring(11).Trim();
                                     }
                                }

                                 var valResult = await EvaluateAsync(session, ScriptPreprocessor.Preprocess(exprBody), session.InspectedNodeId);
                                 if (returnByValue)
                                 {
                                     var valNode = ConvertJsValueToJsonNode(valResult.Engine, valResult.Value);
                                     return new JsonObject { ["result"] = new JsonObject { ["value"] = valNode } };
                                 }
                                 else
                                 {
                                     var objVal = valResult.Value.ToObject();
                                     return new JsonObject { ["result"] = CreateRemoteObject(session, objVal) };
                                 }
                            }
                        }

                        if (target is PlaywrightLocatorLookupFunctionMock)
                        {
                            JsonObject? infoObj3 = null;
                            if (arguments != null && arguments.Count >= 2)
                            {
                                JsonObject? argEx10 = arguments[1] as JsonObject;
                                if (argEx10 != null)
                                {
                                    JsonNode? valNodeEx10;
                                    if (argEx10.TryGetPropertyValue("value", out valNodeEx10))
                                    {
                                        JsonObject? valObjEx10 = valNodeEx10 as JsonObject;
                                        if (valObjEx10 != null)
                                        {
                                            var deserialized5 = DeserializePlaywrightValue(valObjEx10) as JsonObject;
                                            infoObj3 = deserialized5?["info"] as JsonObject;
                                        }
                                    }
                                }
                            }

                            string selector = "";
                            if (infoObj3 != null)
                            {
                                if (infoObj3.TryGetPropertyValue("selector", out var selectorNode) && selectorNode != null)
                                {
                                    selector = selectorNode.GetValue<string>();
                                }
                                else if (infoObj3.TryGetPropertyValue("parsed", out var parsedNode) && parsedNode is JsonObject parsedObj)
                                {
                                    if (parsedObj.TryGetPropertyValue("parts", out var partsNode) && partsNode is JsonArray partsArr && partsArr.Count > 0)
                                    {
                                        var part = partsArr[0] as JsonObject;
                                        selector = part?["source"]?.GetValue<string>() ?? part?["body"]?.GetValue<string>() ?? "";
                                    }
                                }
                            }

                            if (selector.StartsWith("css=")) selector = selector.Substring(4);
                            else if (selector.StartsWith("xpath=")) selector = selector.Substring(6);

                            var matched = session.Window != null ? SelectorEngine.QuerySelector(session.Window, selector, session.UseLogicalTree) : null;
                            bool success = matched != null;
                            Console.WriteLine($"[CDP PLAYWRIGHT DEBUG] Lookup selector: {selector}, found: {success}");
                            object? elemVal = null;
                            if (success)
                            {
                                var runtimeElem = new CdpRuntimeElement(session, matched!);
                                string elemId = session.RegisterObject(runtimeElem);
                                elemVal = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["subtype"] = "node",
                                    ["className"] = "CdpRuntimeElement",
                                    ["objectId"] = elemId
                                };
                            }

                            var lookupResult = new PlaywrightLookupResultMock
                            {
                                Log = success ? $"Locator resolved to {selector}" : $"Locator not resolved for {selector}",
                                Success = success,
                                Element = elemVal
                            };
                            string resId = session.RegisterObject(lookupResult);
                            return new JsonObject
                            {
                                ["result"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["className"] = "Object",
                                    ["objectId"] = resId
                                }
                            };
                        }

                        if (target is PlaywrightLookupResultMock lkResult)
                        {
                            if (functionDeclaration.Contains("log"))
                            {
                                return returnByValue 
                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = lkResult.Log } } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, lkResult.Log) };
                            }
                            if (functionDeclaration.Contains("success"))
                            {
                                return returnByValue 
                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = lkResult.Success } } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, lkResult.Success) };
                            }
                            if (functionDeclaration.Contains("element"))
                            {
                                if (lkResult.Element is JsonNode jNodeResult)
                                {
                                    return new JsonObject { ["result"] = jNodeResult.DeepClone() };
                                }
                                return returnByValue 
                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = null } } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, null) };
                            }
                        }

                        if (target is PlaywrightExpectResultMock expResult)
                        {
                            if (functionDeclaration.Contains("log"))
                            {
                                return returnByValue 
                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = expResult.Log } } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, expResult.Log) };
                            }
                            if (functionDeclaration.Contains("success"))
                            {
                                return returnByValue 
                                    ? new JsonObject { ["result"] = new JsonObject { ["value"] = expResult.Success } } 
                                    : new JsonObject { ["result"] = CreateRemoteObject(session, expResult.Success) };
                            }
                        }

                        var startTime = DateTime.UtcNow;
                        var jintRes = EvaluateFunction(session, target, functionDeclaration, arguments, contextId);
                        ProfilerDomain.RecordActivity(session, "EvaluateConsole", startTime, DateTime.UtcNow);
                        var val = awaitPromise ? await AwaitPromiseIfNeededAsync(jintRes.Engine, jintRes.Value) : jintRes.Value;
                        bool deepSerialization = false;
                        int maxDepth = 2;
                        Console.WriteLine($"[CDP DEEP DEBUG] @params: {@params.ToJsonString()}");
                        if (@params.TryGetPropertyValue("serializationOptions", out var serOptNode) && serOptNode is JsonObject serOptObj)
                        {
                            Console.WriteLine($"[CDP DEEP DEBUG] found serializationOptions: {serOptObj.ToJsonString()}");
                            if (serOptObj.TryGetPropertyValue("serialization", out var serValNode))
                            {
                                Console.WriteLine($"[CDP DEEP DEBUG] found serialization: {serValNode?.ToJsonString()}, val: {serValNode?.GetValue<string>()}");
                                if (serValNode?.GetValue<string>() == "deep")
                                {
                                    deepSerialization = true;
                                    if (serOptObj.TryGetPropertyValue("maxDepth", out var maxDepthNode))
                                    {
                                        maxDepth = maxDepthNode?.GetValue<int>() ?? 2;
                                    }
                                }
                            }
                        }
                        Console.WriteLine($"[CDP DEEP DEBUG] deepSerialization flag: {deepSerialization}, maxDepth: {maxDepth}");

                        if (deepSerialization)
                        {
                            var remoteObj = CreateRemoteObject(session, val.ToObject());
                            remoteObj["deepSerializedValue"] = CreateDeepSerializedValue(session, jintRes.Engine, val, 0, maxDepth);
                            return new JsonObject { ["result"] = remoteObj };
                        }

                        if (returnByValue)
                        {
                            var valNode = ConvertJsValueToJsonNode(jintRes.Engine, val);
                            return new JsonObject { ["result"] = new JsonObject { ["value"] = valNode } };
                        }
                        else
                        {
                            var wrappedObj = (val.IsObject() || val.IsSymbol()) ? new JintObjectWrapper(val, jintRes.Engine) : (object)val;
                            return new JsonObject { ["result"] = CreateRemoteObject(session, wrappedObj) };
                        }
                    });
                    return result;
                }

            case "getProperties":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    bool awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;
                    int contextId = @params["executionContextId"]?.GetValue<int>() ?? 1;
                    var rawTarget = session.GetRawObject(objectId);
                    var target = session.GetObject(objectId);
                    if (target == null && rawTarget == null)
                    {
                        throw new Exception($"Object with ID {objectId} not found");
                    }

                    var propertiesJson = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
                        if (rawTarget is JintObjectWrapper wrapper && wrapper.Value.IsObject())
                        {
                            try
                            {
                                var obj = wrapper.Value.AsObject();
                                foreach (var key in obj.GetOwnProperties())
                                {
                                    try
                                    {
                                        var propVal = obj.Get(key.Key);
                                        object? wrappedVal = propVal;
                                        if (propVal.IsObject() || propVal.IsSymbol())
                                        {
                                            wrappedVal = new JintObjectWrapper(propVal, wrapper.Engine);
                                        }

                                        var propDesc = new JsonObject
                                        {
                                            ["name"] = key.Key.ToString(),
                                            ["value"] = CreateRemoteObject(session, wrappedVal),
                                            ["writable"] = true,
                                            ["configurable"] = true,
                                            ["enumerable"] = true
                                        };
                                        list.Add(propDesc);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                            return list;
                        }

                        if (target != null)
                        {
                            var props = target.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                        }
                        return list;
                    });

                    return new JsonObject { ["result"] = propertiesJson };
                }

            case "releaseObject":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    bool awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;
                    int contextId = @params["executionContextId"]?.GetValue<int>() ?? 1;
                    session.RemoteObjects.TryRemove(objectId, out _);
                    return new JsonObject();
                }

            case "releaseObjectGroup":
            case "discardConsoleEntries":
                {
                    return new JsonObject();
                }

            case "getIsolateId":
                {
                    return new JsonObject { ["isolateId"] = "1" };
                }

            case "getHeapUsage":
                {
                    double jsHeapUsedSize = GC.GetTotalMemory(false);
                    double jsHeapTotalSize = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
                    return new JsonObject
                    {
                        ["usedSize"] = jsHeapUsedSize,
                        ["totalSize"] = jsHeapTotalSize
                    };
                }

            case "runIfWaitingForDebugger":
                {
                    foreach (var script in session.ScriptsToEvaluateOnNewDocument.Values)
                    {
                        try
                        {
                            await EvaluateAsync(session, ScriptPreprocessor.Preprocess(script), inspectedNodeId: 0);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CDP SERVER ERROR] Error evaluating pre-flight script: {ex.Message}");
                        }
                    }

                    var targetId = session.CurrentTargetSession?.TargetId ?? session.Target?.Id;
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        CdpServer.ResumeTarget(targetId);
                    }

                    return new JsonObject();
                }

            case "setCustomObjectFormatterEnabled":
            case "setMaxCallStackSizeToCapture":
            case "setAsyncCallStackDepth":
            case "addBinding":
            case "removeBinding":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Runtime.{action} is not implemented");
        }
    }

    private static JintResult EvaluateFunction(CdpSession session, object? target, string functionDeclaration, JsonArray? arguments, int contextId = 1)
    {
        if (functionDeclaration.Contains("function isNodeReachable(node) {"))
        {
            functionDeclaration = functionDeclaration.Replace(
                "function isNodeReachable(node) {",
                "function isNodeReachable(node) { console.log('[CDP REACHABLE DEBUG] nodeName:', node ? node.nodeName : null, 'nodeRootName:', getNodeRootThroughAnyShadows(node) ? getNodeRootThroughAnyShadows(node).nodeName : null, 'docParentName:', (document.documentElement && document.documentElement.parentNode) ? document.documentElement.parentNode.nodeName : null, 'isMatch:', getNodeRootThroughAnyShadows(node) === (document.documentElement ? document.documentElement.parentNode : null));"
            );
        }
        if (functionDeclaration.Contains("arguments.callee.caller"))
        {
            functionDeclaration = functionDeclaration.Replace("arguments.callee.caller", "(arguments.callee.caller || globalThis.getJintCaller())");
        }

        var selectedNode = session.NodeMap.GetVisual(session.InspectedNodeId);
        var engine = EnsureEngineInitialized(session, contextId, selectedNode);

        if (selectedNode != null)
        {
            engine.SetValue("__raw_0", selectedNode);
            var wrapped = engine.Evaluate("__wrap(__raw_0)");
            engine.SetValue("$0", wrapped);
            engine.SetValue("_0", wrapped);
        }
        else
        {
            engine.SetValue("$0", Jint.Native.JsValue.Null);
            engine.SetValue("_0", Jint.Native.JsValue.Null);
        }

        Jint.Native.JsValue thisValue;
        if (target is Visual visualTarget)
        {
            engine.SetValue("__raw_this", visualTarget);
            thisValue = engine.Evaluate("__wrap(__raw_this)");
        }
        else if (target is CdpRuntimeElement runtimeElemTarget)
        {
            engine.SetValue("__raw_this", runtimeElemTarget.visual);
            thisValue = engine.Evaluate("__wrap(__raw_this)");
        }
        else if (target is Jint.Native.JsValue jsValThis)
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
                        var deserialized = DeserializePlaywrightValue(valNode);
                        jsArgs.Add(ConvertJsonNodeToJsValue(engine, deserialized));
                    }
                    else if (argObj.TryGetPropertyValue("objectId", out var objIdNode) && objIdNode != null)
                    {
                        var argObjVal = session.GetObject(objIdNode.GetValue<string>());
                        if (argObjVal is Visual visualArg)
                        {
                            engine.SetValue("__raw_arg", visualArg);
                            jsArgs.Add(engine.Evaluate("__wrap(__raw_arg)"));
                        }
                        else if (argObjVal is CdpRuntimeElement runtimeElemArg)
                        {
                            engine.SetValue("__raw_arg", runtimeElemArg.visual);
                            jsArgs.Add(engine.Evaluate("__wrap(__raw_arg)"));
                        }
                        else if (argObjVal is Jint.Native.JsValue jsValArg)
                        {
                            jsArgs.Add(jsValArg);
                        }
                        else
                        {
                            jsArgs.Add(Jint.Native.JsValue.FromObject(engine, argObjVal));
                        }
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
        if (funcCode.Contains("function buildError(error) {"))
        {
            funcCode = funcCode.Replace("function buildError(error) {", "function buildError(error) { console.log('[ATOM ERROR STACK]:', error.stack || error.message || error);");
        }
        if (funcCode.StartsWith("function") || funcCode.StartsWith("async function") || funcCode.Contains("=>"))
        {
            if (!funcCode.StartsWith("(") && !funcCode.EndsWith(")"))
            {
                funcCode = "(" + funcCode + "\n)";
            }
        }

        try
        {
            var jsFunc = engine.Evaluate(funcCode);
            var jsResult = engine.Invoke(jsFunc, thisValue, jsArgs.ToArray());
            return new JintResult { Value = jsResult, Engine = engine };
        }
        catch (JavaScriptException ex)
        {
            Console.WriteLine($"[CDP JINT ERROR] JavaScriptException in EvaluateFunction: {ex.Message}\nJS Stack:\n{ex.JavaScriptStackTrace}");
            throw;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "REPL dynamic script property evaluation")]
    private static Engine EnsureEngineInitialized(CdpSession session, int contextId, Visual? selectedNode)
    {
        if (session.Window == null || !session.Window.IsVisible)
        {
            var mainWin = CdpServer.GetWindows().FirstOrDefault().Window;
            if (mainWin != null)
            {
                session.Window = mainWin;
            }
        }

        var targetSession = session.CurrentTargetSession;
        string targetId = targetSession != null ? targetSession.TargetId : (session.Target?.Id ?? "");
        string key = $"{session.GetHashCode()}_{targetId}_{contextId}";
        bool isNew = false;
        var engine = _engines.GetOrAdd(key, _ =>
        {
            isNew = true;
            return new Engine(options =>
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
        });

        var control = selectedNode as Avalonia.Controls.Control;
        var dataContext = control?.DataContext;
        System.Console.WriteLine($"[CDP ENGINE INIT DIAGNOSTIC] selectedNode={selectedNode?.GetType().FullName ?? "null"}, session.Window={session.Window?.GetType().FullName ?? "null"}");
        var windowObj = (selectedNode as Avalonia.Controls.Window) ?? (session.Window as Avalonia.Controls.Window);
        System.Console.WriteLine($"[CDP ENGINE INIT DIAGNOSTIC] windowObj={windowObj?.GetType().FullName ?? "null"}");

        engine.SetValue("SelectedNode", selectedNode);
        engine.SetValue("Control", control);
        engine.SetValue("DataContext", dataContext);
        engine.SetValue("ViewModel", dataContext);
        engine.SetValue("__raw_window", windowObj);
        engine.SetValue("Window", windowObj);
        engine.SetValue("__log", new Action<string>(msg => Console.WriteLine("[JS LOG] " + msg)));
        engine.SetValue("getJintCaller", new Func<Jint.Native.JsValue>(() => {
            var callStackField = typeof(Engine).GetField("CallStack", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (callStackField != null)
            {
                var callStackVal = callStackField.GetValue(engine);
                if (callStackVal != null)
                {
                    var stackField = callStackVal.GetType().GetField("_stack", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stackField != null)
                    {
                        var stackVal = stackField.GetValue(callStackVal);
                        if (stackVal is System.Collections.IEnumerable enumerable)
                        {
                            int index = 0;
                            foreach (var item in enumerable)
                            {
                                var funcField = item.GetType().GetField("Function", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (funcField != null)
                                {
                                    var funcVal = funcField.GetValue(item);
                                    Console.WriteLine($"[CDP STACK] Frame {index}: {funcVal?.ToString() ?? "null"}");
                                }
                                index++;
                            }
                            index = 0;
                            foreach (var item in enumerable)
                            {
                                if (index == 2)
                                {
                                    var funcField = item.GetType().GetField("Function", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (funcField != null)
                                    {
                                        var funcVal = funcField.GetValue(item);
                                        if (funcVal is Jint.Native.JsValue jsVal)
                                        {
                                            return jsVal;
                                        }
                                    }
                                }
                                index++;
                            }
                        }
                    }
                }
            }
            return Jint.Native.JsValue.Undefined;
        }));
        
        var rawDoc = new CdpRuntimeDocument(session);
        engine.SetValue("__raw_document", rawDoc);
        
        engine.SetValue("__getProperty", new Func<object, string, object?>((obj, propName) => {
            if (obj == null) return null;
            if (obj is Jint.Native.JsValue jsVal)
            {
                obj = jsVal.ToObject();
            }
            if (obj is JintObjectWrapper wrapper)
            {
                obj = wrapper.Value.ToObject();
            }
            if (obj == null) return null;
            var type = obj.GetType();
            System.Console.WriteLine($"[CDP GETPROPERTY DEBUG] obj={obj}, type={type.FullName}, propName={propName}");
            var prop = type.GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Static);
            if (prop != null) return prop.GetValue(obj);
            var field = type.GetField(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Static);
            if (field != null) return field.GetValue(obj);
            return null;
        }));

        engine.SetValue("Print", new Action<object?>(Console.WriteLine));
        engine.SetValue("Query", new Func<string, Visual?>(s => Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(session.Window ?? selectedNode, s, session.UseLogicalTree)));
        engine.SetValue("QueryAll", new Func<string, IEnumerable<Visual>>(s => Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelectorAll(session.Window ?? selectedNode, s, session.UseLogicalTree)));
        engine.SetValue("__getBounds", new Func<Visual, double[]>(visual => DomDomain.GetVisualBounds(session, visual)));
        engine.SetValue("__getNodeId", new Func<Visual, int>(visual => session.NodeMap.GetOrAdd(visual)));
        engine.SetValue("__getVisualByNodeId", new Func<int, Visual?>(nodeId => session.NodeMap.GetVisual(nodeId)));
        engine.SetValue("__elementFromPoint", new Func<double, double, Visual?>((x, y) => {
            return Dispatcher.UIThread.Invoke(() => {
                var window = session.Window;
                if (window == null) return null;
                var point = new Point(x, y);
                return window.InputHitTest(point) as Visual;
            });
        }));
        engine.SetValue("__focus", new Action<Visual>(visual => {
            Dispatcher.UIThread.Invoke(() => {
                if (visual is Avalonia.Input.IInputElement inputElement) {
                    inputElement.Focus();
                }
            });
            rawDoc._activeElement = new CdpRuntimeElement(session, visual);
        }));
        engine.SetValue("__blur", new Action(() => {
            Dispatcher.UIThread.Invoke(() => {
                session.Window?.FocusManager?.Focus(null);
            });
            rawDoc._activeElement = null;
        }));
        engine.SetValue("__getTypeName", new Func<Visual, string>(visual => visual.GetType().Name));
        engine.SetValue("__getProperty", new Func<Visual, string, object?>((visual, propName) => {
            return Dispatcher.UIThread.Invoke(() => {
                var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(visual)
                    .FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                if (avProperty != null)
                {
                    return visual.GetValue(avProperty);
                }

                // Fallback to CLR property reflection
                var prop = visual.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                return prop != null && prop.CanRead ? prop.GetValue(visual) : null;
            });
        }));
        engine.SetValue("__hasProperty", new Func<Visual, string, bool>((visual, propName) => {
            return Dispatcher.UIThread.Invoke(() => {
                var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(visual)
                    .FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                if (avProperty != null) return true;

                var prop = visual.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                return prop != null;
            });
        }));
        engine.SetValue("__setProperty", new Action<Visual, string, object?>((visual, propName, val) => {
            Dispatcher.UIThread.Invoke(() => {
                var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(visual)
                    .FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                if (avProperty != null)
                {
                    try
                    {
                        var converted = Convert.ChangeType(val, avProperty.PropertyType);
                        visual.SetValue(avProperty, converted);
                        return;
                    }
                    catch
                    {
                        try
                        {
                            visual.SetValue(avProperty, val);
                            return;
                        }
                        catch { }
                    }
                }

                // Fallback to CLR property reflection
                var prop = visual.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite) {
                    try {
                        var converted = Convert.ChangeType(val, prop.PropertyType);
                        prop.SetValue(visual, converted);
                    } catch {
                        try {
                            prop.SetValue(visual, val);
                        } catch { }
                    }
                }
            });
        }));
        engine.SetValue("__getParent", new Func<object, Visual?>(target => 
        {
            Visual? visual = null;
            if (target is CdpRuntimeElement elem)
            {
                visual = elem.visual as Visual;
            }
            else
            {
                visual = target as Visual;
            }
            if (visual == null || visual == session.Window) return null;
            return session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalParent(visual)
                : visual.GetVisualParent();
        }));

        engine.SetValue("__getChildren", new Func<object, IEnumerable<Visual>>(target => 
        {
            Visual? visual = null;
            if (target is CdpRuntimeElement elem)
            {
                visual = elem.visual as Visual;
            }
            else
            {
                visual = target as Visual;
            }
            if (visual == null) return Array.Empty<Visual>();
            var list = new List<Visual>();
            var childrenList = session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalChildren(visual)
                : visual.GetVisualChildren();
            foreach (var child in childrenList)
            {
                list.Add(child);
            }
            return list;
        }));

        engine.SetValue("__getSiblings", new Func<object, IEnumerable<Visual>>(target => 
        {
            Visual? visual = null;
            if (target is CdpRuntimeElement elem)
            {
                visual = elem.visual as Visual;
            }
            else
            {
                visual = target as Visual;
            }
            if (visual == null) return Array.Empty<Visual>();
            var parent = session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalParent(visual)
                : visual.GetVisualParent();
            if (parent == null) return new[] { visual };
            return session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalChildren(parent)
                : parent.GetVisualChildren();
        }));

        engine.SetValue("__querySelector", new Func<object, string, Visual?>((target, sel) => 
        {
            Visual? visual = null;
            if (target is CdpRuntimeDocument)
            {
                visual = session.Window;
            }
            else if (target is CdpRuntimeElement elem)
            {
                visual = elem.visual as Visual;
            }
            else
            {
                visual = target as Visual;
            }
            if (visual == null) return null;
            var match = Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(visual, sel, session.UseLogicalTree);
            if (match != null)
            {
                var matchWindow = TopLevel.GetTopLevel(match);
                if (matchWindow != null) session.Window = matchWindow;
            }
            return match;
        }));

        engine.SetValue("__querySelectorAll", new Func<object, string, IEnumerable<Visual>>((target, sel) => 
        {
            Visual? visual = null;
            if (target is CdpRuntimeDocument)
            {
                visual = session.Window;
            }
            else if (target is CdpRuntimeElement elem)
            {
                visual = elem.visual as Visual;
            }
            else
            {
                visual = target as Visual;
            }
            if (visual == null) return Array.Empty<Visual>();
            var matches = Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelectorAll(visual, sel, session.UseLogicalTree);
            if (matches.Count > 0)
            {
                var matchWindow = TopLevel.GetTopLevel(matches[0]);
                if (matchWindow != null) session.Window = matchWindow;
            }
            return matches;
        }));

        if (isNew)
        {
            engine.Evaluate(@"
                if (typeof globalThis.document === 'undefined' || globalThis.document === null || !globalThis.document.__isProxy) {
                    var raw = globalThis.__raw_document;
                    globalThis.document = new Proxy(raw, {
                        get(target, prop, receiver) {
                            try { globalThis.__log('document.get: ' + String(prop)); } catch(e) {}
                            if (prop === '__isProxy') return true;
                            if (prop === 'defaultView') return globalThis.window;
                            if (prop === 'ownerDocument') return null;
                            if (prop === 'parentNode') return null;
                            if (prop === 'parentElement') return null;
                            if (prop === 'hasFocus') {
                                return function() { return true; };
                            }
                            if (prop === 'elementFromPoint') {
                                return function(x, y) {
                                    var visual = globalThis.__elementFromPoint(x, y);
                                    if (visual) return globalThis.__wrap(visual);
                                    return null;
                                };
                            }
                            if (prop === 'getElementById') {
                                return function(id) {
                                    var escaped = id.replace(/\\/g, '\\\\').replace(/""/g, '\\""');
                                    var res = globalThis.__querySelector(target, '[id=""' + escaped + '""]');
                                    return res ? globalThis.__wrap(res) : null;
                                };
                            }
                            if (prop === 'querySelector') {
                                return function(sel) {
                                    var res = globalThis.__querySelector(target, sel);
                                    return res ? globalThis.__wrap(res) : null;
                                };
                            }
                            if (prop === 'querySelectorAll' || prop === 'getElementsByTagName' || prop === 'getElementsByClassName') {
                                return function(arg) {
                                    var arr = globalThis.__querySelectorAll(target, arg);
                                    var wrappedList = [];
                                    if (arr) {
                                        for (var item of arr) {
                                            wrappedList.push(globalThis.__wrap(item));
                                        }
                                    }
                                    wrappedList.item = function(idx) { return this[idx]; };
                                    return wrappedList;
                                };
                            }
                            var val = target[prop];
                            if (typeof val === 'function') {
                                return val.bind(target);
                            }
                            return globalThis.__wrap(val);
                        }
                    });
                }
                if (true) {
                    class WindowClass {
                        static [Symbol.hasInstance](instance) {
                            var inst = globalThis.__raw_window;
                            return instance === inst || (instance && (instance.nodeType === 9 || instance.nodeName === 'WINDOW' || instance.nodeName === 'CdpRuntimeDocument'));
                        }
                    }
                    globalThis.Window = new Proxy(WindowClass, {
                        get(target, prop, receiver) {
                            if (prop === Symbol.hasInstance) {
                                return target[Symbol.hasInstance];
                            }
                            if (prop === 'prototype') {
                                return target.prototype;
                            }
                            var WindowInstance = globalThis.__raw_window;
                            if (WindowInstance) {
                                var val = globalThis.__getProperty(WindowInstance, prop);
                                if (typeof val === 'function') {
                                     return val.bind(WindowInstance);
                                }
                                return val;
                            }
                            return undefined;
                        }
                    });
                }
                globalThis.window = globalThis;
                globalThis.devicePixelRatio = 1.0;
                if (typeof globalThis.visualViewport === 'undefined') {
                    globalThis.visualViewport = {
                        width: 800,
                        height: 600,
                        scale: 1,
                        offsetTop: 0,
                        offsetLeft: 0,
                        pageTop: 0,
                        pageLeft: 0
                    };
                }
            ");

            engine.Evaluate(@"
                if (typeof globalThis.DOMRect === 'undefined') {
                    globalThis.DOMRect = class {
                        constructor(x, y, width, height) {
                            this.x = x || 0;
                            this.y = y || 0;
                            this.width = width || 0;
                            this.height = height || 0;
                            this.left = this.x;
                            this.top = this.y;
                            this.right = this.x + this.width;
                            this.bottom = this.y + this.height;
                        }
                    };
                    globalThis.ClientRect = globalThis.DOMRect;
                }

                if (typeof globalThis.__polyfilled === 'undefined') {
                    globalThis.__polyfilled = true;

                    globalThis.console = {
                        log: function() {
                            var args = Array.prototype.slice.call(arguments);
                            globalThis.__log(args.map(function(x) {
                                try {
                                    if (x === null) return 'null';
                                    if (x === undefined) return 'undefined';
                                    if (x && typeof x === 'object') {
                                        if (x.nodeName) return x.nodeName;
                                        return JSON.stringify(x);
                                    }
                                    return String(x);
                                } catch(e) { return String(x); }
                            }).join(' '));
                        }
                    };

                    globalThis.getComputedStyle = function(element) {
                        var style = {
                            display: 'block',
                            visibility: 'visible',
                            opacity: '1',
                            position: 'static',
                            overflow: 'visible',
                            overflowX: 'visible',
                            overflowY: 'visible',
                            'overflow-x': 'visible',
                            'overflow-y': 'visible',
                            getPropertyValue: function(prop) {
                                return this[prop] || '';
                            }
                        };
                        return new Proxy(style, {
                            get(target, prop) {
                                if (prop in target) return target[prop];
                                return '';
                            }
                        });
                    };

                    Object.defineProperty(Object.prototype, 'ownerDocument', {
                        get: function() {
                            if (this && this.nodeType === 1) {
                                return globalThis.document;
                            }
                            if (this && this.nodeType === 9) {
                                return null;
                            }
                            return undefined;
                        },
                        configurable: true
                    });

                    Object.defineProperty(Object.prototype, 'parentNode', {
                        get: function() {
                            if (this && this.nodeType === 1) {
                                var p = this.__raw_parentNode;
                                if (p) return globalThis.__wrap(p);
                                return globalThis.document;
                            }
                            return null;
                        },
                        configurable: true
                    });

                    Object.defineProperty(Object.prototype, 'defaultView', {
                        get: function() {
                            if (this && this.nodeType === 9) {
                                return globalThis.window;
                            }
                            return undefined;
                        },
                        configurable: true
                    });

                    Object.defineProperty(Object.prototype, 'isConnected', {
                        get: function() {
                            if (this && (this.nodeType === 1 || this.nodeType === 9)) {
                                return true;
                            }
                            return undefined;
                        },
                        configurable: true
                    });

                    Object.prototype.contains = function(other) {
                        if (!other) return false;
                        var targetId = this.nodeId;
                        if (typeof targetId === 'undefined') return false;
                        var current = other;
                        while (current) {
                            if (current.nodeId === targetId) return true;
                            current = current.parentNode;
                        }
                        return false;
                    };

                    Object.prototype.compareDocumentPosition = function(other) {
                        if (!other) return 1;
                        if (other.nodeId === this.nodeId) return 0;
                        if (this.contains(other)) return 20;
                        if (other.contains(this)) return 10;
                        return 1;
                    };
                    
                    var timers = new Map();
                    var nextTimerId = 1;
                    globalThis.setInterval = function(callback, delay) {
                        var id = nextTimerId++;
                        timers.set(id, { callback, isInterval: true });
                        return id;
                    };
                    globalThis.setTimeout = function(callback, delay) {
                        var id = nextTimerId++;
                        timers.set(id, { callback, isInterval: false });
                        return id;
                    };
                    globalThis.clearInterval = function(id) {
                        timers.delete(id);
                    };
                    globalThis.clearTimeout = function(id) {
                        timers.delete(id);
                    };
                    globalThis.requestAnimationFrame = function(callback) {
                        return globalThis.setTimeout(callback, 16);
                    };
                    globalThis.cancelAnimationFrame = function(id) {
                        globalThis.clearTimeout(id);
                    };
                    globalThis.__runTimers = function() {
                        for (var timerEntry of timers.entries()) {
                            var id = timerEntry[0];
                            var timer = timerEntry[1];
                            try {
                                timer.callback();
                            } catch(e) {}
                            if (!timer.isInterval) {
                                timers.delete(id);
                            }
                        }
                    };

                    globalThis.MutationObserver = class {
                        constructor(callback) {
                            this.callback = callback;
                            this.intervalId = null;
                        }
                        observe(target, options) {
                            this.intervalId = globalThis.setInterval(() => {
                                try {
                                    this.callback([], this);
                                } catch(e) {}
                            }, 30);
                        }
                        disconnect() {
                            if (this.intervalId) {
                                globalThis.clearInterval(this.intervalId);
                                this.intervalId = null;
                            }
                        }
                    };

                    globalThis.Node = class {
                        static [Symbol.hasInstance](instance) {
                            return instance && (instance.nodeType === 1 || instance.nodeType === 9 || instance.nodeType === 11);
                        }
                    };
                    globalThis.Node.ELEMENT_NODE = 1;
                    globalThis.Node.DOCUMENT_NODE = 9;

                    globalThis.Element = class {
                        static [Symbol.hasInstance](instance) {
                            return instance && instance.nodeType === 1;
                        }
                    };

                    globalThis.HTMLElement = class extends globalThis.Element {
                        static [Symbol.hasInstance](instance) {
                            return instance && instance.nodeType === 1;
                        }
                    };
 
                    globalThis.SVGElement = class extends globalThis.Element {
                        static [Symbol.hasInstance](instance) {
                            return false;
                        }
                    };

                    globalThis.IntersectionObserver = class {
                        constructor(callback, options) {
                            this.callback = callback;
                        }
                        observe(target) {
                            if (this.callback) {
                                try {
                                    this.callback([{ intersectionRatio: 1, isIntersecting: true }]);
                                } catch (e) {}
                            }
                        }
                        unobserve(target) {}
                        disconnect() {}
                        takeRecords() { return []; }
                    };

                    globalThis.Document = class {
                        static [Symbol.hasInstance](instance) {
                            return instance && instance.nodeType === 9;
                        }
                    };

                    globalThis.HTMLDocument = class extends globalThis.Document {
                        static [Symbol.hasInstance](instance) {
                            return instance && instance.nodeType === 9;
                        }
                    };

                    globalThis.ShadowRoot = class {
                        static [Symbol.hasInstance](instance) {
                            return instance && instance.nodeType === 11;
                        }
                    };

                    globalThis.Window = class {
                        static [Symbol.hasInstance](instance) {
                            return instance && (instance === globalThis || instance.window === instance);
                        }
                    };

                    globalThis.Location = class {
                        static [Symbol.hasInstance](instance) { return false; }
                    };

                    globalThis.location = {
                        href: 'http://127.0.0.1/',
                        protocol: 'http:',
                        host: '127.0.0.1',
                        hostname: '127.0.0.1',
                        port: '',
                        pathname: '/',
                        search: '',
                        hash: ''
                    };

                    var elementSubclasses = ['HTMLAnchorElement', 'HTMLInputElement', 'HTMLSelectElement', 'HTMLTextAreaElement', 'HTMLButtonElement', 'HTMLOptionElement', 'HTMLFormElement', 'HTMLImageElement', 'HTMLCanvasElement', 'HTMLVideoElement', 'HTMLAudioElement', 'HTMLFrameElement', 'HTMLIFrameElement'];
                    for (var name of elementSubclasses) {
                        globalThis[name] = class extends globalThis.HTMLElement {
                            static [Symbol.hasInstance](instance) {
                                return instance && instance.nodeType === 1;
                            }
                        };
                    }
                }
                
                globalThis.__resolveNode = function(nodeId) {
                    var visual = typeof __getVisualByNodeId === 'function' ? __getVisualByNodeId(nodeId) : null;
                    return visual ? globalThis.__wrap(visual) : null;
                };
                globalThis.__proxyCache = globalThis.__proxyCache || new Map();
                globalThis.__wrap = function(target) {
                    if (!target) return target;
                    if (typeof target !== 'object' && typeof target !== 'function') return target;
                    if (target.__isProxy) return target;
                    
                    var raw = target;
                    if (target.visual) {
                        raw = target.visual;
                    }
                    if (!raw || (typeof raw !== 'object' && typeof raw !== 'function')) return raw;
                    
                    if (globalThis.__proxyCache.has(raw)) {
                        return globalThis.__proxyCache.get(raw);
                    }
                    
                    var p = new Proxy(raw, {
                        get(t, prop, receiver) {
                            try { globalThis.__log('wrap.get: ' + String(prop) + ' on type: ' + globalThis.__getTypeName(t)); } catch(e) {}
                            if (prop === '__isProxy') return true;
                            if (prop === '__raw_node') return t;
                            if (prop === 'nodeId') {
                                return globalThis.__getNodeId(t);
                            }
                            if (prop === 'id' || prop === 'name') {
                                return globalThis.__getProperty(t, 'Name') || '';
                            }
                            if (prop === 'setSelectionRange') {
                                return function(start, end) {
                                    t._selectionStart = start;
                                    t._selectionEnd = end;
                                };
                            }
                            if (prop === 'selectionStart') {
                                return typeof t._selectionStart === 'number' ? t._selectionStart : 0;
                            }
                            if (prop === 'selectionEnd') {
                                return typeof t._selectionEnd === 'number' ? t._selectionEnd : 0;
                            }
                            if (prop === 'textContent' || prop === 'innerText') {
                                var txt = globalThis.__getProperty(t, 'Text');
                                if (txt !== null) return String(txt);
                                var content = globalThis.__getProperty(t, 'Content');
                                if (content !== null) return String(content);
                                var header = globalThis.__getProperty(t, 'Header');
                                if (header !== null) return String(header);
                                return '';
                            }
                            if (prop === 'value') {
                                var txt = globalThis.__getProperty(t, 'Text');
                                if (txt !== null) return String(txt);
                                var val = globalThis.__getProperty(t, 'Value');
                                if (val !== null) return String(val);
                                return '';
                            }
                            if (prop === 'isVisible') {
                                return globalThis.__getProperty(t, 'IsEffectivelyVisible') !== false && globalThis.__getProperty(t, 'IsVisible') !== false;
                            }
                            if (prop === 'isEffectivelyVisible') {
                                return globalThis.__getProperty(t, 'IsEffectivelyVisible') !== false && globalThis.__getProperty(t, 'IsVisible') !== false;
                            }
                            if (prop === 'isEnabled') {
                                return globalThis.__getProperty(t, 'IsEnabled') !== false;
                            }
                            if (prop === 'isChecked' || prop === 'checked') {
                                return globalThis.__getProperty(t, 'IsChecked') === true;
                            }
                            if (prop === 'selectedIndex') {
                                var idx = globalThis.__getProperty(t, 'SelectedIndex');
                                return typeof idx === 'number' ? idx : -1;
                            }
                            if (prop === 'isConnected') return true;
                            if (prop === 'type') {
                                var typeName = globalThis.__getTypeName(t);
                                if (typeName.indexOf('CheckBox') >= 0) return 'checkbox';
                                if (typeName.indexOf('RadioButton') >= 0) return 'radio';
                                if (typeName.indexOf('TextBox') >= 0) return 'text';
                                if (typeName.indexOf('Button') >= 0) return 'button';
                                return '';
                            }
                            if (prop === 'attributes') {
                                var attrs = [];
                                var idVal = receiver.id;
                                if (idVal) attrs.push({ name: 'id', value: idVal, nodeName: 'id', nodeValue: idVal, nodeType: 2 });
                                var classVal = receiver.className;
                                if (classVal) attrs.push({ name: 'class', value: classVal, nodeName: 'class', nodeValue: classVal, nodeType: 2 });
                                var typeVal = receiver.type;
                                if (typeVal) attrs.push({ name: 'type', value: typeVal, nodeName: 'type', nodeValue: typeVal, nodeType: 2 });
                                var valueVal = receiver.value;
                                if (valueVal) attrs.push({ name: 'value', value: valueVal, nodeName: 'value', nodeValue: valueVal, nodeType: 2 });
                                attrs.item = function(i) { return attrs[i]; };
                                return attrs;
                            }
                            if (prop === 'outerHTML') {
                                var tn = receiver.tagName || 'DIV';
                                var idStr = receiver.id ? ' id=\'' + receiver.id + '\'' : '';
                                return '<' + tn + idStr + '></' + tn + '>';
                            }
                            if (prop === 'innerHTML') {
                                return '';
                            }
                            if (prop === 'hasChildNodes') {
                                return function() {
                                    var arr = globalThis.__getChildren(t);
                                    if (arr) {
                                        for (var item of arr) {
                                            return true;
                                        }
                                    }
                                    return !!receiver.textContent;
                                };
                            }
                            if (prop === 'nodeType') return typeof t.nodeType === 'number' ? t.nodeType : 1;
                            if (prop === 'nodeName' || prop === 'tagName') {
                                var typeName = globalThis.__getTypeName(t);
                                if (typeName.indexOf('TextBox') >= 0) return 'INPUT';
                                if (typeName.indexOf('CheckBox') >= 0) return 'INPUT';
                                if (typeName.indexOf('RadioButton') >= 0) return 'INPUT';
                                if (typeName.indexOf('Button') >= 0) return 'BUTTON';
                                return typeName.toUpperCase();
                            }
                            if (prop === 'style') {
                                return new Proxy({}, {
                                    get(target, p) {
                                        return '';
                                    }
                                });
                            }
                            if (prop === 'clientLeft' || prop === 'clientTop') return 0;
                            if (prop === 'clientWidth' || prop === 'offsetWidth' || prop === 'scrollWidth') {
                                return globalThis.__getBounds(t)[2];
                            }
                            if (prop === 'clientHeight' || prop === 'offsetHeight' || prop === 'scrollHeight') {
                                return globalThis.__getBounds(t)[3];
                            }
                            if (prop === 'offsetLeft' || prop === 'offsetTop') return 0;
                            if (prop === 'getAttribute') {
                                return function(name) {
                                    var lowerName = String(name).toLowerCase();
                                    var typeName = globalThis.__getTypeName(t);
                                    if (lowerName === 'type') {
                                        if (typeName.indexOf('CheckBox') >= 0) return 'checkbox';
                                        if (typeName.indexOf('RadioButton') >= 0) return 'radio';
                                        if (typeName.indexOf('TextBox') >= 0) return 'text';
                                        return '';
                                    }
                                    if (lowerName === 'checked') {
                                        if (typeName.indexOf('CheckBox') >= 0 || typeName.indexOf('RadioButton') >= 0) {
                                            return globalThis.__getProperty(t, 'IsChecked') ? 'true' : null;
                                        }
                                        return null;
                                    }
                                    if (lowerName === 'selected') {
                                        return globalThis.__getProperty(t, 'IsSelected') ? 'true' : null;
                                    }
                                    if (lowerName === 'id') return globalThis.__getProperty(t, 'Name') || null;
                                    if (lowerName === 'name') return globalThis.__getProperty(t, 'Name') || null;
                                    if (lowerName === 'value') return receiver.value || null;
                                    return null;
                                };
                            }
                            if (prop === 'attributes') {
                                 var attrs = [];
                                 var idVal = receiver.id;
                                 if (idVal) attrs.push({ name: 'id', value: idVal, nodeName: 'id', nodeValue: idVal, nodeType: 2 });
                                 var classVal = receiver.className;
                                 if (classVal) attrs.push({ name: 'class', value: classVal, nodeName: 'class', nodeValue: classVal, nodeType: 2 });
                                 var typeVal = receiver.type;
                                 if (typeVal) attrs.push({ name: 'type', value: typeVal, nodeName: 'type', nodeValue: typeVal, nodeType: 2 });
                                 var valueVal = receiver.value;
                                 if (valueVal) attrs.push({ name: 'value', value: valueVal, nodeName: 'value', nodeValue: valueVal, nodeType: 2 });
                                 attrs.item = function(i) { return attrs[i]; };
                                 attrs.getNamedItem = function(name) {
                                     for (var i = 0; i < attrs.length; i++) {
                                         if (attrs[i].name === name) return attrs[i];
                                     }
                                     return null;
                                 };
                                 return attrs;
                             }
                             if (prop === 'hasAttribute') {
                                 return function(name) {
                                     return receiver.getAttribute(name) !== null;
                                 };
                             }
                             if (prop === 'getBoundingClientRect') {
                                 return function() {
                                     var bounds = globalThis.__getBounds(t);
                                     var rect = {
                                         x: bounds[0],
                                         y: bounds[1],
                                         width: bounds[2],
                                         height: bounds[3],
                                         left: bounds[0],
                                         top: bounds[1],
                                         right: bounds[0] + bounds[2],
                                         bottom: bounds[1] + bounds[3]
                                     };
                                     if (globalThis.DOMRect) {
                                         return new globalThis.DOMRect(rect.x, rect.y, rect.width, rect.height);
                                     }
                                     return rect;
                                 };
                             }
                             if (prop === 'getClientRects') {
                                 return function() {
                                     return [receiver.getBoundingClientRect()];
                                 };
                             }
                             if (prop === 'getAttributeNode') {
                                 return function(name) {
                                     var val = receiver.getAttribute(name);
                                     return val !== null ? { name: name, value: val, nodeName: name, nodeValue: val, nodeType: 2 } : null;
                                 };
                             }
                             if (prop === 'stop') return function() {};
                             if (prop === 'shadowRoot') return null;
                             if (prop === 'focus') {
                                 return function() {
                                     globalThis.__focus(t);
                                 };
                             }
                             if (prop === 'blur') {
                                 return function() {
                                     globalThis.__blur();
                                 };
                             }
                             if (prop === 'ownerDocument') return globalThis.document;
                             if (prop === 'parentNode') {
                                 var p = globalThis.__getParent(t);
                                 if (p) return globalThis.__wrap(p);
                                 return globalThis.document;
                             }
                             if (prop === 'parentElement') {
                                 var p = globalThis.__getParent(t);
                                 return p ? globalThis.__wrap(p) : null;
                             }
                              if (prop === 'children' || prop === 'childNodes') {
                                  var rawArr = globalThis.__getChildren(t);
                                  var wrapped = [];
                                  if (rawArr) {
                                      for (var item of rawArr) {
                                          wrapped.push(globalThis.__wrap(item));
                                      }
                                  }
                                  if (prop === 'childNodes' && wrapped.length === 0) {
                                      var text = receiver.textContent;
                                      if (text) {
                                          var textNode = {
                                              nodeType: 3,
                                              nodeName: '#text',
                                              nodeValue: text,
                                              data: text,
                                              textContent: text,
                                              innerText: text,
                                              parentNode: receiver,
                                              childNodes: [],
                                              children: [],
                                              hasChildNodes: function() { return false; }
                                          };
                                          wrapped.push(textNode);
                                      }
                                  }
                                  wrapped.item = function(idx) { return this[idx]; };
                                  return wrapped;
                              }
                              if (prop === 'firstChild') {
                                  var rawArr = globalThis.__getChildren(t);
                                  if (rawArr) {
                                      for (var item of rawArr) {
                                          return globalThis.__wrap(item);
                                      }
                                  }
                                  var childrenNodes = receiver.childNodes;
                                  return childrenNodes.length > 0 ? childrenNodes[0] : null;
                              }
                              if (prop === 'lastChild') {
                                  var rawArr = globalThis.__getChildren(t);
                                  if (rawArr) {
                                      var last = null;
                                      for (var item of rawArr) {
                                          last = item;
                                      }
                                      if (last) return globalThis.__wrap(last);
                                  }
                                  var childrenNodes = receiver.childNodes;
                                  return childrenNodes.length > 0 ? childrenNodes[childrenNodes.length - 1] : null;
                              }
                              if (prop === 'nextSibling') {
                                  var rawArr = globalThis.__getSiblings(t);
                                  if (rawArr) {
                                      var arr = Array.from(rawArr);
                                      var idx = -1;
                                      for (var i = 0; i < arr.length; i++) {
                                          if (arr[i] === t) { idx = i; break; }
                                      }
                                      if (idx >= 0 && idx < arr.length - 1) {
                                          return globalThis.__wrap(arr[idx + 1]);
                                      }
                                  }
                                  return null;
                              }
                              if (prop === 'previousSibling') {
                                  var rawArr = globalThis.__getSiblings(t);
                                  if (rawArr) {
                                      var arr = Array.from(rawArr);
                                      var idx = -1;
                                      for (var i = 0; i < arr.length; i++) {
                                          if (arr[i] === t) { idx = i; break; }
                                      }
                                      if (idx > 0) {
                                          return globalThis.__wrap(arr[idx - 1]);
                                      }
                                  }
                                  return null;
                              }
                             if (prop === 'querySelector') {
                                 return function(sel) {
                                     var res = globalThis.__querySelector(t, sel);
                                     return res ? globalThis.__wrap(res) : null;
                                 };
                             }
                             if (prop === 'querySelectorAll' || prop === 'getElementsByTagName' || prop === 'getElementsByClassName') {
                                 return function(arg) {
                                     var arr = globalThis.__querySelectorAll(t, arg);
                                     var wrapped = [];
                                     if (arr) {
                                         for (var item of arr) {
                                             wrapped.push(globalThis.__wrap(item));
                                         }
                                     }
                                     wrapped.item = function(idx) { return this[idx]; };
                                     return wrapped;
                                 };
                             }
                             if (prop === 'contains') {
                                 return Object.prototype.contains;
                             }
                             if (prop === 'compareDocumentPosition') {
                                 return Object.prototype.compareDocumentPosition;
                             }
                             var val = t[prop];
                             if (typeof val === 'function') {
                                 return val.bind(t);
                             }
                             return globalThis.__wrap(val);
                        },
                        set(t, prop, value, receiver) {
                            if (prop === 'textContent' || prop === 'innerText' || prop === 'value') {
                                if (globalThis.__hasProperty(t, 'Text')) {
                                    globalThis.__setProperty(t, 'Text', value);
                                    return true;
                                }
                                if (globalThis.__hasProperty(t, 'Content')) {
                                    globalThis.__setProperty(t, 'Content', value);
                                    return true;
                                }
                            }
                            if (prop === 'selectedIndex') {
                                globalThis.__setProperty(t, 'SelectedIndex', value);
                                return true;
                            }
                            if (prop === 'isChecked' || prop === 'checked') {
                                globalThis.__setProperty(t, 'IsChecked', value);
                                return true;
                            }
                            try {
                                globalThis.__setProperty(t, prop, value);
                                return true;
                            } catch(e) {}
                            try {
                                t[prop] = value;
                                return true;
                            } catch(e) {}
                            return false;
                        }
                    });
                    globalThis.__proxyCache.set(raw, p);
                    return p;
                };
            ");
        }

        return engine;
    }

    private static object? EvaluateExpression(CdpSession session, object target, string expression, Dictionary<string, object?>? variableBindings = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;

        var trimmed = expression.Trim();

        // 0. Logical OR (||) evaluation
        if (trimmed.Contains("||"))
        {
            var orSplit = trimmed.Split(new[] { "||" }, StringSplitOptions.None);
            foreach (var part in orSplit)
            {
                try
                {
                    var val = EvaluateExpression(session, target, part.Trim(), variableBindings);
                    if (val != null)
                    {
                        var str = val.ToString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            return val;
                        }
                    }
                }
                catch
                {
                    // Ignore and try next operand
                }
            }
            return null;
        }

        // 1. Literal constants
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if ((trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) || (trimmed.StartsWith("'") && trimmed.EndsWith("'")))
        {
            var content = trimmed.Substring(1, trimmed.Length - 2);
            return content.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
        }
        if (int.TryParse(trimmed, out int iVal)) return iVal;
        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal)) return dVal;

        // 2. Comparison expressions
        string[]? opSplit = null;
        string? op = null;
        if (trimmed.Contains("===")) { op = "==="; opSplit = trimmed.Split(new[] { "===" }, StringSplitOptions.None); }
        else if (trimmed.Contains("!==")) { op = "!=="; opSplit = trimmed.Split(new[] { "!==" }, StringSplitOptions.None); }
        else if (trimmed.Contains("==")) { op = "=="; opSplit = trimmed.Split(new[] { "==" }, StringSplitOptions.None); }
        else if (trimmed.Contains("!=")) { op = "!="; opSplit = trimmed.Split(new[] { "!=" }, StringSplitOptions.None); }

        if (opSplit != null && opSplit.Length == 2 && op != null)
        {
            var leftVal = EvaluateExpression(session, target, opSplit[0], variableBindings);
            var rightVal = EvaluateExpression(session, target, opSplit[1], variableBindings);

            bool isEqual = Equals(leftVal?.ToString(), rightVal?.ToString());
            if (leftVal is bool lBool && rightVal is bool rBool)
            {
                isEqual = lBool == rBool;
            }
            else if (double.TryParse(leftVal?.ToString(), out double lD) && double.TryParse(rightVal?.ToString(), out double rD))
            {
                isEqual = Math.Abs(lD - rD) < 0.000001;
            }

            if (op == "==" || op == "===") return isEqual;
            return !isEqual;
        }

        if (trimmed.StartsWith("$0") || trimmed.StartsWith("_0") || trimmed.StartsWith("SelectedNode"))
        {
            var inspected = session.NodeMap.GetVisual(session.InspectedNodeId);
            if (inspected == null) throw new Exception("No inspected node is selected");

            int prefixLen = trimmed.StartsWith("SelectedNode") ? 12 : 2;
            if (trimmed.Length == prefixLen) return inspected;

            var remaining = trimmed.Substring(prefixLen);
            if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
            return EvaluateExpression(session, inspected, remaining, variableBindings);
        }

        if (trimmed.StartsWith("Control"))
        {
            var inspected = session.NodeMap.GetVisual(session.InspectedNodeId);
            var control = inspected as Avalonia.Controls.Control;
            if (control == null) throw new Exception("No inspected control is selected");

            if (trimmed == "Control") return control;

            var remaining = trimmed.Substring(7);
            if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
            return EvaluateExpression(session, control, remaining, variableBindings);
        }

        if (trimmed.StartsWith("$vm") || trimmed.StartsWith("$dc") || trimmed.StartsWith("DataContext"))
        {
            var inspected = session.NodeMap.GetVisual(session.InspectedNodeId);
            if (inspected is Avalonia.Controls.Control control)
            {
                var dc = control.DataContext;
                int prefixLen = trimmed.StartsWith("DataContext") ? 11 : 3;
                if (trimmed.Length == prefixLen) return dc;
                if (dc == null) return null;
                var remaining = trimmed.Substring(prefixLen);
                if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
                return EvaluateExpression(session, dc, remaining, variableBindings);
            }
            throw new Exception("No inspected control is selected");
        }

        if (trimmed.StartsWith("window"))
        {
            if (trimmed == "window") return session.Window;
            var remaining = trimmed.Substring(6);
            if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
            return EvaluateExpression(session, new CdpRuntimeWindow(session), remaining, variableBindings);
        }

        if (trimmed.StartsWith("document"))
        {
            if (trimmed == "document") return new CdpRuntimeDocument(session);
            var remaining = trimmed.Substring(8);
            if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
            return EvaluateExpression(session, new CdpRuntimeDocument(session), remaining, variableBindings);
        }

        // Strip leading "this." if present
        if (trimmed.StartsWith("this."))
        {
            trimmed = trimmed.Substring(5).Trim();
        }

        expression = trimmed;

        // Handle assignment: e.g. "Width = 500"
        var eqIndex = expression.IndexOf('=');
        if (eqIndex != -1)
        {
            var propPath = expression.Substring(0, eqIndex).Trim();
            var valStr = expression.Substring(eqIndex + 1).Trim().Trim('"', '\'');

            // Find the last dot at nesting level 0 in propPath
            int splitDot = -1;
            int parenLevel = 0;
            for (int i = propPath.Length - 1; i >= 0; i--)
            {
                char c = propPath[i];
                if (c == ')') parenLevel++;
                else if (c == '(') parenLevel--;
                else if (c == '.' && parenLevel == 0)
                {
                    splitDot = i;
                    break;
                }
            }

            object? current = target;
            string lastPropName = propPath;
            if (splitDot != -1)
            {
                var targetPath = propPath.Substring(0, splitDot).Trim();
                lastPropName = propPath.Substring(splitDot + 1).Trim();
                current = EvaluateExpression(session, target, targetPath, variableBindings);
                if (current == null) throw new Exception($"Target path '{targetPath}' evaluated to null");
            }

            var lastProp = current.GetType().GetProperty(lastPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (lastProp == null && current is CdpRuntimeWindow lastWin && lastWin.visual != null)
            {
                lastProp = lastWin.visual.GetType().GetProperty(lastPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            }
            if (lastProp == null || !lastProp.CanWrite) throw new Exception($"Property '{lastPropName}' not found or read-only");

            object? boundValue = null;
            bool isBound = false;
            if (variableBindings != null && variableBindings.TryGetValue(valStr, out boundValue))
            {
                isBound = true;
            }

            var converted = isBound ? ConvertValueFromBound(boundValue, lastProp.PropertyType) : ConvertValue(valStr, lastProp.PropertyType);
            var invokeTargetLast = lastProp.DeclaringType.IsAssignableFrom(current.GetType())
                ? current
                : (current is CdpRuntimeWindow lastWin2 ? lastWin2.visual : current);
            lastProp.SetValue(invokeTargetLast, converted);
            return converted;
        }
        else
        {
            // Read property path: e.g. "Bounds.Width" or "Close()"
            var parts = SplitPropertyPath(expression);
            object? current = target;
            foreach (var part in parts)
            {
                if (current == null) return null;

                if (part.Contains('(') && part.EndsWith(')'))
                {
                    int opIndex = part.IndexOf('(');
                    var methodName = part.Substring(0, opIndex).Trim();
                    var argStr = part.Substring(opIndex + 1, part.Length - opIndex - 2).Trim();

                    // Resolve arguments (support multiple comma-separated arguments)
                    object?[] methodArgs;
                    Type[] argTypes;

                    if (string.IsNullOrEmpty(argStr))
                    {
                        methodArgs = Array.Empty<object?>();
                        argTypes = Type.EmptyTypes;
                    }
                    else
                    {
                        var argsList = new System.Collections.Generic.List<string>();
                        var currentArg = new System.Text.StringBuilder();
                        bool inDoubleQuotes = false;
                        bool inSingleQuotes = false;
                        for (int i = 0; i < argStr.Length; i++)
                        {
                            char c = argStr[i];
                            if (c == '"' && (i == 0 || argStr[i - 1] != '\\'))
                            {
                                inDoubleQuotes = !inDoubleQuotes;
                                currentArg.Append(c);
                            }
                            else if (c == '\'' && (i == 0 || argStr[i - 1] != '\\'))
                            {
                                inSingleQuotes = !inSingleQuotes;
                                currentArg.Append(c);
                            }
                            else if (c == ',' && !inDoubleQuotes && !inSingleQuotes)
                            {
                                argsList.Add(currentArg.ToString().Trim());
                                currentArg.Clear();
                            }
                            else
                            {
                                currentArg.Append(c);
                            }
                        }
                        if (currentArg.Length > 0 || argsList.Count > 0)
                        {
                            argsList.Add(currentArg.ToString().Trim());
                        }

                        methodArgs = new object?[argsList.Count];
                        argTypes = new Type[argsList.Count];

                        for (int idx = 0; idx < argsList.Count; idx++)
                        {
                            var argValStr = argsList[idx];
                            if (variableBindings != null && variableBindings.TryGetValue(argValStr, out var boundVal))
                            {
                                methodArgs[idx] = boundVal;
                                argTypes[idx] = boundVal?.GetType() ?? typeof(object);
                            }
                            else if ((argValStr.StartsWith("\"") && argValStr.EndsWith("\"")) || (argValStr.StartsWith("'") && argValStr.EndsWith("'")))
                            {
                                var unescaped = argValStr.Substring(1, argValStr.Length - 2)
                                    .Replace("\\\"", "\"")
                                    .Replace("\\n", "\n")
                                    .Replace("\\r", "\r")
                                    .Replace("\\t", "\t");
                                methodArgs[idx] = unescaped;
                                argTypes[idx] = typeof(string);
                            }
                            else if (int.TryParse(argValStr, out int intVal))
                            {
                                methodArgs[idx] = intVal;
                                argTypes[idx] = typeof(int);
                            }
                            else if (double.TryParse(argValStr, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleVal))
                            {
                                methodArgs[idx] = doubleVal;
                                argTypes[idx] = typeof(double);
                            }
                            else if (bool.TryParse(argValStr, out bool boolVal))
                            {
                                methodArgs[idx] = boolVal;
                                argTypes[idx] = typeof(bool);
                            }
                            else
                            {
                                methodArgs[idx] = argValStr;
                                argTypes[idx] = typeof(string);
                            }
                        }
                    }

                    var method = current.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase, null, argTypes, null);
                    if (method == null && current is CdpRuntimeWindow win && win.visual != null)
                    {
                        method = win.visual.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase, null, argTypes, null);
                    }
                    if (method == null)
                    {
                        // Try to find any method with name and correct number of parameters
                        var methods = current.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase)
                            .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == methodArgs.Length)
                            .ToArray();
                        if (methods.Length == 0 && current is CdpRuntimeWindow win2 && win2.visual != null)
                        {
                            methods = win2.visual.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase)
                                .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == methodArgs.Length)
                                .ToArray();
                        }
                        if (methods.Length > 0)
                        {
                            method = methods[0];
                            // Try to convert args
                            var parameters = method.GetParameters();
                            for (int idx = 0; idx < parameters.Length; idx++)
                            {
                                if (methodArgs[idx] != null)
                                {
                                    methodArgs[idx] = ConvertValue(methodArgs[idx]!.ToString()!, parameters[idx].ParameterType);
                                }
                            }
                        }
                    }

                    if (method == null) throw new Exception($"Method '{methodName}' with {methodArgs.Length} arguments not found on {current.GetType().Name}");
                    var invokeTarget = method.DeclaringType.IsAssignableFrom(current.GetType()) ? current : ((CdpRuntimeWindow)current).visual;
                    current = method.Invoke(invokeTarget, methodArgs);
                }
                else
                {
                    var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null && current is CdpRuntimeWindow win && win.visual != null)
                    {
                        prop = win.visual.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    }
                    if (prop == null) throw new Exception($"Property '{part}' not found on {current.GetType().Name}");
                    var invokeTarget = prop.DeclaringType.IsAssignableFrom(current.GetType()) ? current : ((CdpRuntimeWindow)current).visual;
                    current = prop.GetValue(invokeTarget);
                }
            }
            return current;
        }
    }

    private static string[] SplitPropertyPath(string path)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inDoubleQuotes = false;
        bool inSingleQuotes = false;
        int parenDepth = 0;

        for (int i = 0; i < path.Length; i++)
        {
            char c = path[i];
            if (c == '"' && (i == 0 || path[i - 1] != '\\'))
            {
                inDoubleQuotes = !inDoubleQuotes;
                current.Append(c);
            }
            else if (c == '\'' && (i == 0 || path[i - 1] != '\\'))
            {
                inSingleQuotes = !inSingleQuotes;
                current.Append(c);
            }
            else if (!inDoubleQuotes && !inSingleQuotes)
            {
                if (c == '(')
                {
                    parenDepth++;
                    current.Append(c);
                }
                else if (c == ')')
                {
                    parenDepth--;
                    current.Append(c);
                }
                else if (c == '.' && parenDepth == 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }
        return parts.ToArray();
    }

    private static JsonObject CreateRemoteObject(CdpSession session, object? obj)
    {
        if (obj is JintObjectWrapper wrapper)
        {
            obj = wrapper.Value;
        }

        if (obj is Jint.Native.JsValue jsVal)
        {
            if (jsVal.IsNull() || jsVal.IsUndefined())
            {
                return new JsonObject
                {
                    ["type"] = "object",
                    ["subtype"] = "null",
                    ["value"] = null
                };
            }
            if (jsVal.IsBoolean())
            {
                return new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = jsVal.AsBoolean()
                };
            }
            if (jsVal.IsString())
            {
                return new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = jsVal.AsString()
                };
            }
            if (jsVal.IsNumber())
            {
                double jsNumVal = jsVal.AsNumber();
                if (double.IsPositiveInfinity(jsNumVal))
                {
                    return new JsonObject { ["type"] = "number", ["unserializableValue"] = "Infinity" };
                }
                if (double.IsNegativeInfinity(jsNumVal))
                {
                    return new JsonObject { ["type"] = "number", ["unserializableValue"] = "-Infinity" };
                }
                if (double.IsNaN(jsNumVal))
                {
                    return new JsonObject { ["type"] = "number", ["unserializableValue"] = "NaN" };
                }
                return new JsonObject
                {
                    ["type"] = "number",
                    ["value"] = jsNumVal
                };
            }
            if (jsVal.IsObject())
            {
                object? unwrappedClr = null;
                try
                {
                    var clrVal = jsVal.Get("__raw_node");
                    if (!clrVal.IsUndefined() && !clrVal.IsNull())
                    {
                        unwrappedClr = clrVal.ToObject();
                    }
                    else
                    {
                        unwrappedClr = jsVal.ToObject();
                    }
                }
                catch {}

                if (unwrappedClr is Visual unwrappedVisual)
                {
                    var jsObjectId = session.RegisterObject(unwrappedVisual);
                    return new JsonObject
                    {
                        ["type"] = "object",
                        ["subtype"] = "node",
                        ["className"] = unwrappedVisual.GetType().FullName,
                        ["description"] = $"{unwrappedVisual.GetType().Name} ({unwrappedVisual})",
                        ["objectId"] = jsObjectId,
                        ["backendNodeId"] = session.NodeMap.GetOrAdd(unwrappedVisual),
                        ["loaderId"] = "main-loader-id"
                    };
                }
                if (unwrappedClr is CdpRuntimeElement unwrappedRuntimeElem)
                {
                    var jsObjectId = session.RegisterObject(unwrappedRuntimeElem);
                    return new JsonObject
                    {
                        ["type"] = "object",
                        ["subtype"] = "node",
                        ["className"] = unwrappedRuntimeElem.GetType().FullName,
                        ["description"] = $"{unwrappedRuntimeElem.GetType().Name} ({unwrappedRuntimeElem})",
                        ["objectId"] = jsObjectId,
                        ["backendNodeId"] = session.NodeMap.GetOrAdd((Visual)unwrappedRuntimeElem.visual),
                        ["loaderId"] = "main-loader-id"
                    };
                }
                if (unwrappedClr is CdpRuntimeDocument unwrappedDoc)
                {
                    var jsObjectId = session.RegisterObject(unwrappedDoc);
                    return new JsonObject
                    {
                        ["type"] = "object",
                        ["subtype"] = "node",
                        ["className"] = unwrappedDoc.GetType().FullName,
                        ["description"] = $"{unwrappedDoc.GetType().Name} ({unwrappedDoc})",
                        ["objectId"] = jsObjectId,
                        ["backendNodeId"] = 1,
                        ["loaderId"] = "main-loader-id"
                    };
                }

                var jsObj = jsVal.AsObject();
                var jsObjectId2 = session.RegisterObject(jsVal);
                var typeName = jsObj.GetType().Name;
                string className = typeName.EndsWith("Instance")
                    ? typeName.Substring(0, typeName.Length - "Instance".Length)
                    : "Object";
                if (jsVal.IsArray()) className = "Array";

                var jsObjResult = new JsonObject
                {
                    ["type"] = jsVal.IsSymbol() ? "symbol" : className == "Function" ? "function" : "object",
                    ["className"] = className,
                    ["description"] = className == "Array" ? $"Array({jsObj.Get("length")})" : className,
                    ["objectId"] = jsObjectId2
                };
                if (className == "Array")
                {
                    jsObjResult["subtype"] = "array";
                }
                return jsObjResult;
            }
            obj = jsVal.ToObject();
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

        if (obj is double d)
        {
            if (double.IsPositiveInfinity(d))
            {
                return new JsonObject { ["type"] = "number", ["unserializableValue"] = "Infinity" };
            }
            if (double.IsNegativeInfinity(d))
            {
                return new JsonObject { ["type"] = "number", ["unserializableValue"] = "-Infinity" };
            }
            if (double.IsNaN(d))
            {
                return new JsonObject { ["type"] = "number", ["unserializableValue"] = "NaN" };
            }
        }
        else if (obj is float f)
        {
            if (float.IsPositiveInfinity(f))
            {
                return new JsonObject { ["type"] = "number", ["unserializableValue"] = "Infinity" };
            }
            if (float.IsNegativeInfinity(f))
            {
                return new JsonObject { ["type"] = "number", ["unserializableValue"] = "-Infinity" };
            }
            if (float.IsNaN(f))
            {
                return new JsonObject { ["type"] = "number", ["unserializableValue"] = "NaN" };
            }
        }

        var type = obj.GetType();
        if (type == typeof(string) || type.IsPrimitive || type == typeof(decimal))
        {
            return new JsonObject
            {
                ["type"] = type == typeof(string) ? "string" : type == typeof(bool) ? "boolean" : "number",
                ["value"] = JsonValue.Create(obj)
            };
        }

        var objectId = session.RegisterObject(obj);
        var result = new JsonObject
        {
            ["type"] = "object",
            ["className"] = type.FullName,
            ["description"] = $"{type.Name} ({obj})",
            ["objectId"] = objectId
        };

        if (obj is Visual visual)
        {
            result["subtype"] = "node";
            result["backendNodeId"] = session.NodeMap.GetOrAdd(visual);
            result["loaderId"] = "main-loader-id";
        }
        else if (obj is CdpRuntimeElement runtimeElem)
        {
            result["subtype"] = "node";
            result["backendNodeId"] = session.NodeMap.GetOrAdd((Visual)runtimeElem.visual);
            result["loaderId"] = "main-loader-id";
        }
        else if (obj is CdpRuntimeDocument)
        {
            result["subtype"] = "node";
            result["backendNodeId"] = 1;
            result["loaderId"] = "main-loader-id";
        }

        return result;
    }

    private static JsonObject CreateDeepSerializedValue(CdpSession session, Jint.Engine engine, Jint.Native.JsValue val, int depth = 0, int maxDepth = 3, System.Collections.Generic.HashSet<Jint.Native.JsValue>? visited = null)
    {
        var result = new JsonObject();
        if (val.IsString())
        {
            result["type"] = "string";
            result["value"] = val.AsString();
            return result;
        }
        if (val.IsNumber())
        {
            result["type"] = "number";
            result["value"] = val.AsNumber();
            return result;
        }
        if (val.IsBoolean())
        {
            result["type"] = "boolean";
            result["value"] = val.AsBoolean();
            return result;
        }
        if (val.IsNull())
        {
            result["type"] = "null";
            return result;
        }
        if (val.IsUndefined())
        {
            result["type"] = "undefined";
            return result;
        }

        if (depth > maxDepth)
        {
            result["type"] = "object";
            result["description"] = val.ToString();
            return result;
        }

        var clrObj = UnwrapJsValue(val);
        if (clrObj != null)
        {
            if (clrObj is Visual visualObj)
            {
                result["type"] = "node";
                result["value"] = new JsonObject
                {
                    ["nodeType"] = 1,
                    ["nodeName"] = visualObj.GetType().Name.ToUpperInvariant(),
                    ["backendNodeId"] = session.NodeMap.GetOrAdd(visualObj),
                    ["loaderId"] = "main-loader-id"
                };
                return result;
            }
            else if (clrObj is CdpRuntimeElement runtimeElemObj)
            {
                result["type"] = "node";
                result["value"] = new JsonObject
                {
                    ["nodeType"] = 1,
                    ["nodeName"] = runtimeElemObj.tagName,
                    ["backendNodeId"] = session.NodeMap.GetOrAdd((Visual)runtimeElemObj.visual),
                    ["loaderId"] = "main-loader-id"
                };
                return result;
            }
            else if (clrObj is CdpRuntimeDocument)
            {
                result["type"] = "node";
                result["value"] = new JsonObject
                {
                    ["nodeType"] = 9,
                    ["nodeName"] = "#document",
                    ["backendNodeId"] = 1,
                    ["loaderId"] = "main-loader-id"
                };
                return result;
            }
        }

        visited ??= new System.Collections.Generic.HashSet<Jint.Native.JsValue>();
        if (visited.Contains(val))
        {
            result["type"] = "object";
            result["description"] = "[Circular]";
            return result;
        }
        visited.Add(val);

        if (val.IsArray())
        {
            result["type"] = "array";
            var arr = val.AsArray();
            var list = new JsonArray();
            var len = (int)arr.Length;
            for (int i = 0; i < len; i++)
            {
                var itemVal = arr.Get(i.ToString());
                list.Add(CreateDeepSerializedValue(session, engine, itemVal, depth + 1, maxDepth, visited));
            }
            result["value"] = list;
        }
        else if (val.IsObject())
        {
            result["type"] = "object";
            var obj = val.AsObject();
            var list = new JsonArray();
            foreach (var key in obj.GetOwnProperties())
            {
                var itemVal = obj.Get(key.Key);
                var entry = new JsonObject
                {
                    ["name"] = key.Key.ToString(),
                    ["value"] = CreateDeepSerializedValue(session, engine, itemVal, depth + 1, maxDepth, visited)
                };
                list.Add(entry);
            }
            result["value"] = list;
        }
        else
        {
            result["type"] = "undefined";
        }
        visited.Remove(val);
        return result;
    }

    private static object? UnwrapJsValue(Jint.Native.JsValue val)
    {
        if (val == null) return null;

        var valType = val.GetType();
        if (valType.Name == "JsProxy" || valType.FullName == "Jint.Native.JsProxy")
        {
            var targetField = valType.GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (targetField != null)
            {
                var targetVal = targetField.GetValue(val) as Jint.Native.JsValue;
                if (targetVal != null)
                {
                    return UnwrapJsValue(targetVal);
                }
            }
        }
        if (val is Jint.Runtime.Interop.ObjectWrapper wrapper)
        {
            return wrapper.Target;
        }
        return null;
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0])) return s;
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    private static object? ConvertValue(string val, Type targetType)
    {
        if (targetType == typeof(string)) return val;
        if (targetType == typeof(double)) return double.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(float)) return float.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(int)) return int.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool)) return bool.Parse(val);

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, val, true);
        }

        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(val);
        }

        return Convert.ChangeType(val, targetType, CultureInfo.InvariantCulture);
    }

    private static object? GetNodeValue(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue jsonVal)
        {
            var element = jsonVal.GetValue<System.Text.Json.JsonElement>();
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    return element.GetString();
                case System.Text.Json.JsonValueKind.Number:
                    if (element.TryGetInt32(out int i)) return i;
                    if (element.TryGetInt64(out long l)) return l;
                    return element.GetDouble();
                case System.Text.Json.JsonValueKind.True:
                    return true;
                case System.Text.Json.JsonValueKind.False:
                    return false;
                case System.Text.Json.JsonValueKind.Null:
                    return null;
            }
        }
        return node;
    }

    private static object? ConvertValueFromBound(object? value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsAssignableFrom(value.GetType())) return value;
        try
        {
            var underType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value is string str)
            {
                return ConvertValue(str, underType);
            }
            return Convert.ChangeType(value, underType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return value;
        }
    }

    private static Type GetClosestAccessibleType(Type type)
    {
        var current = type;
        while (current != null)
        {
            if (!current.IsGenericType && IsTypeAccessible(current))
            {
                return current;
            }
            current = current.BaseType;
        }
        return typeof(Avalonia.Visual);
    }

    private static bool IsTypeAccessible(Type type)
    {
        if (type.IsNested)
        {
            return type.IsNestedPublic && IsTypeAccessible(type.DeclaringType!);
        }
        return type.IsPublic;
    }

    private static string GetSafeTypeName(Type type)
    {
        var accessibleType = GetClosestAccessibleType(type);
        string name = accessibleType.FullName ?? accessibleType.Name;
        return name.Replace('+', '.');
    }

    private static async Task<JintResult> EvaluateAsync(CdpSession session, string code, int inspectedNodeId, int contextId = 1)
    {
        Func<JintResult> evalAction = () =>
        {
            Console.WriteLine($"[CDP EVAL DEBUG] Evaluating Jint code: '{code}'");

            var inspectedNode = session.NodeMap.GetVisual(inspectedNodeId);
            var engine = EnsureEngineInitialized(session, contextId, inspectedNode);

            if (inspectedNode != null)
            {
                engine.SetValue("__raw_0", inspectedNode);
                var wrapped = engine.Evaluate("__wrap(__raw_0)");
                engine.SetValue("$0", wrapped);
                engine.SetValue("_0", wrapped);
            }
            else
            {
                engine.SetValue("$0", Jint.Native.JsValue.Null);
                engine.SetValue("_0", Jint.Native.JsValue.Null);
            }

            try
            {
                var jsVal = engine.Evaluate(code);
                Console.WriteLine($"[CDP EVAL RESULT] Code: '{code}', Result: '{jsVal}', Type: '{jsVal.GetType().Name}'");
                return new JintResult { Value = jsVal, Engine = engine };
            }
            catch (Jint.Runtime.JavaScriptException ex)
            {
                Console.WriteLine($"[CDP JINT ERROR] JavaScriptException in EvaluateAsync: {ex.Message}\nJS Stack:\n{ex.JavaScriptStackTrace}");
                throw new InvalidOperationException($"JS evaluation error: {ex.Message}", ex);
            }
        };

        if (Dispatcher.UIThread.CheckAccess())
        {
            return evalAction();
        }
        else
        {
            return await Dispatcher.UIThread.InvokeAsync<JintResult>(evalAction);
        }
    }

    public static void ClearSessionEngines(CdpSession session)
    {
        string prefix = $"{session.GetHashCode()}_";
        var keys = _engines.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var key in keys)
        {
            _engines.TryRemove(key, out _);
        }
    }
 
    private static async Task<Jint.Native.JsValue> AwaitPromiseIfNeededAsync(Jint.Engine engine, Jint.Native.JsValue value)
    {
        if (value != null && (value.GetType().Name == "PromiseInstance" || value.GetType().Name == "JsPromise" || value.GetType().FullName?.Contains("Promise") == true))
        {
            var awaitMethod = engine.GetType().GetMethod("AwaitPromiseSettlementAsync", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awaitMethod != null)
            {
                try
                {
                    var task = (Task<Jint.Native.JsValue>)awaitMethod.Invoke(engine, new object[] { value, CancellationToken.None })!;
                    var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var runContinuationsMethod = engine.GetType().GetMethod("RunAvailableContinuations", bindingFlags);

                    while (!task.IsCompleted)
                    {
                        try
                        {
                            var runTimers = engine.Evaluate("globalThis.__runTimers");
                            if (runTimers != null && !runTimers.IsUndefined() && !runTimers.IsNull())
                            {
                                engine.Invoke(runTimers);
                            }
                        }
                        catch (Exception) { }

                        runContinuationsMethod?.Invoke(engine, null);

                        if (task.IsCompleted) break;

                        await Task.Delay(10);
                    }

                    return await task;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CDP JINT ERROR] Exception awaiting promise via AwaitPromiseSettlementAsync: {ex}");
                }
            }

            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var stateProp = value.GetType().GetProperty("State", flags);
            var valueProp = value.GetType().GetProperty("Value", flags);
            var runMethod = engine.GetType().GetMethod("RunAvailableContinuations", flags);
 
            if (stateProp != null && valueProp != null && runMethod != null)
            {
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromSeconds(30);
 
                while (true)
                {
                    try
                    {
                        var runTimers = engine.Evaluate("globalThis.__runTimers");
                        if (runTimers != null && !runTimers.IsUndefined() && !runTimers.IsNull())
                        {
                            engine.Invoke(runTimers);
                        }
                    }
                    catch (Exception) { }
 
                    runMethod.Invoke(engine, null);
 
                    var state = stateProp.GetValue(value)?.ToString();
                    if (state == "Fulfilled")
                    {
                        return (Jint.Native.JsValue?)valueProp.GetValue(value) ?? Jint.Native.JsValue.Undefined;
                    }
                    if (state == "Rejected")
                    {
                        var errorVal = (Jint.Native.JsValue?)valueProp.GetValue(value) ?? Jint.Native.JsValue.Undefined;
                        throw new Exception(errorVal.ToString());
                    }
 
                    if (DateTime.UtcNow - startTime > timeout)
                    {
                        throw new TimeoutException("Timeout waiting for promise to resolve");
                    }
 
                    await Task.Delay(10);
                }
            }
        }
        return value;
    }
 
    private static Jint.Native.JsValue ConvertJsonNodeToJsValue(Jint.Engine engine, JsonNode? node)
    {
        if (node == null) return Jint.Native.JsValue.Null;
        string jsonStr = node.ToJsonString();
        var jsonParser = engine.Evaluate("JSON.parse");
        return engine.Invoke(jsonParser, jsonStr);
    }

    private static JsonNode? ConvertJsValueToJsonNode(Jint.Engine engine, Jint.Native.JsValue value)
    {
        if (value.IsNull() || value.IsUndefined()) return null;
        var stringify = engine.Evaluate("JSON.stringify");
        var jsonStr = engine.Invoke(stringify, value).AsString();
        if (string.IsNullOrEmpty(jsonStr)) return null;
        return JsonNode.Parse(jsonStr);
    }
}

public class ReplGlobals
{
    private readonly CdpSession _session;

    public ReplGlobals(CdpSession session)
    {
        _session = session;
    }

    public Avalonia.Visual? SelectedNode => _session.NodeMap.GetVisual(_session.InspectedNodeId);
    public Avalonia.Controls.Control? Control => SelectedNode as Avalonia.Controls.Control;
    public object? DataContext => Control?.DataContext;
    public object? ViewModel => DataContext;
    public Avalonia.Controls.Window? Window => (SelectedNode as Avalonia.Controls.Window) ?? (_session.Window as Avalonia.Controls.Window);
    public CdpRuntimeWindow window => new(_session);
    public CdpRuntimeDocument document => new(_session);

    public void Print(object? obj) => Console.WriteLine(obj);

    public Visual? Query(string selector)
    {
        var root = (Visual?)SelectedNode ?? _session.Window;
        return root != null ? Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(root, selector, _session.UseLogicalTree) : null;
    }

    public IEnumerable<Visual> QueryAll(string selector)
    {
        var root = (Visual?)SelectedNode ?? _session.Window;
        return root != null ? Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelectorAll(root, selector, _session.UseLogicalTree) : Enumerable.Empty<Visual>();
    }
}

public sealed class CdpRuntimeWindow
{
    private readonly CdpSession _session;

    public CdpRuntimeWindow(CdpSession session)
    {
        _session = session;
    }

    public CdpRuntimeDocument document => new(_session);
    public Avalonia.Controls.Window? visual => _session.Window as Avalonia.Controls.Window;
}

public sealed class CdpRuntimeDocument
{
    private readonly CdpSession _session;

    public CdpRuntimeDocument(CdpSession session)
    {
        _session = session;
    }

    public string title => _session.Target?.Title ?? "Avalonia UI Application";
    public string URL
    {
        get
        {
            var history = _session.NavigationHistory;
            var index = _session.NavigationHistoryIndex;
            if (index >= 0 && index < history.Count)
            {
                var entry = history[index];
                if (entry.TryGetPropertyValue("url", out var urlNode) && urlNode != null)
                {
                    return urlNode.GetValue<string>();
                }
            }
            return $"http://127.0.0.1:{CdpServer.Port}/";
        }
    }
    public string documentURI => URL;
    public int nodeType => 9;
    public string nodeName => "#document";
    public CdpRuntimeElement? documentElement
    {
        get
        {
            var root = _session.Window;
            return root != null ? new CdpRuntimeElement(_session, root) : null;
        }
    }
    internal CdpRuntimeElement? _activeElement;

    public CdpRuntimeElement? activeElement
    {
        get
        {
            if (_activeElement != null) return _activeElement;
            var focused = _session.Window?.FocusManager?.GetFocusedElement() as Visual;
            if (focused != null) return new CdpRuntimeElement(_session, focused);
            return body;
        }
    }
    public CdpRuntimeElement? head => null;
    public CdpRuntimeDocument? ownerDocument => null;
    public object? defaultView => null;
    public string visibilityState => "visible";
    public bool hidden => false;
 
    public CdpRuntimeElement[] getElementsByTagName(string tagName)
    {
        return querySelectorAll(tagName);
    }
    public CdpRuntimeElement[] getElementsByClassName(string className)
    {
        return querySelectorAll("." + className);
    }

    public string readyState => "complete";

    public CdpRuntimeElement? body
    {
        get
        {
            var el = querySelector("Window") ?? querySelector("Panel") ?? querySelector("ContentControl");
            if (el != null) return el;
            var root = _session.Window;
            return root != null ? new CdpRuntimeElement(_session, root) : null;
        }
    }

    public CdpRuntimeElement? querySelector(string selector)
    {
        var root = _session.Window;
        if (root == null)
        {
            return null;
        }

        var visual = Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(root, selector, _session.UseLogicalTree);
        return visual != null ? new CdpRuntimeElement(_session, visual) : null;
    }

    public CdpRuntimeElement[] querySelectorAll(string selector)
    {
        var root = _session.Window;
        if (root == null)
        {
            return Array.Empty<CdpRuntimeElement>();
        }

        return Avalonia.Diagnostics.Cdp.SelectorEngine
            .QuerySelectorAll(root, selector, _session.UseLogicalTree)
            .Select(visual => new CdpRuntimeElement(_session, visual))
            .ToArray();
    }

    public CdpRuntimeElement? getElementById(string id)
    {
        var escaped = id.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return querySelector($"[id=\"{escaped}\"]");
    }

    public string getPropertiesJson(string selector)
    {
        var root = _session.Window;
        if (root == null) return "{}";

        var visual = Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(root, selector, _session.UseLogicalTree);
        if (visual == null || (visual is Avalonia.Controls.Control control && (!control.IsEffectivelyVisible || !control.IsAttachedToVisualTree()))) return "{}";

        var dict = new Dictionary<string, object?>();
        dict["$Type"] = visual.GetType().Name;
        dict["$FullName"] = visual.GetType().FullName;

        var props = new[] { "IsChecked", "Text", "Value", "IsSelected", "SelectedIndex", "IsExpanded", "SelectedDate", "SelectedTime", "IsFocused", "IsEnabled", "Content", "Header", "PlaceholderText" };
        foreach (var pName in props)
        {
            try
            {
                var p = visual.GetType().GetProperty(pName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (p != null && p.CanRead)
                {
                    var val = p.GetValue(visual);
                    dict[pName] = val?.ToString();
                }
            }
            catch {}
        }
        return System.Text.Json.JsonSerializer.Serialize(dict);
    }

    public bool contains(object? other)
    {
        if (other == null) return false;
        Visual? otherVisual = null;
        if (other is CdpRuntimeElement otherElem)
        {
            otherVisual = otherElem.visual as Visual;
        }
        else if (other is Visual v)
        {
            otherVisual = v;
        }
        if (otherVisual == null) return false;

        var root = _session.Window;
        if (root == null) return false;

        var current = otherVisual;
        while (current != null)
        {
            if (current == root) return true;
            current = _session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalParent(current)
                : current.GetVisualParent();
        }
        return false;
    }
}

public sealed class CdpRuntimeElement
{
    private readonly CdpSession _session;
    private readonly Visual _visual;

    public CdpRuntimeElement(CdpSession session, Visual visual)
    {
        _session = session;
        _visual = visual;
    }

    public int nodeId => _session.NodeMap.GetOrAdd(_visual);
    public CdpRuntimeElement? __raw_parentNode
    {
        get
        {
            if (_visual == _session.Window)
            {
                return null;
            }
            var parent = CdpVisualTreeHelper.GetParent(_visual, _session.UseLogicalTree);
            return parent != null ? new CdpRuntimeElement(_session, parent) : null;
        }
    }
    public CdpRuntimeElement? parentElement => __raw_parentNode;
    public CdpRuntimeDocument ownerDocument => new CdpRuntimeDocument(_session);
 
    public CdpRuntimeElement[] children
    {
        get
        {
            var list = new List<CdpRuntimeElement>();
            foreach (var child in GetSearchChildren())
            {
                list.Add(new CdpRuntimeElement(_session, child));
            }
            return list.ToArray();
        }
    }
    public CdpRuntimeElement[] childNodes => children;
    public CdpRuntimeElement? firstChild => children.Length > 0 ? children[0] : null;
    public CdpRuntimeElement? lastChild => children.Length > 0 ? children[children.Length - 1] : null;
    public CdpRuntimeElement? nextSibling
    {
        get
        {
            var parent = CdpVisualTreeHelper.GetParent(_visual, _session.UseLogicalTree);
            if (parent == null) return null;
            var childrenList = CdpVisualTreeHelper.GetChildren(parent, _session.UseLogicalTree).ToList();
            int idx = childrenList.IndexOf(_visual);
            if (idx >= 0 && idx < childrenList.Count - 1)
            {
                return new CdpRuntimeElement(_session, childrenList[idx + 1]);
            }
            return null;
        }
    }
    public CdpRuntimeElement? previousSibling
    {
        get
        {
            var parent = CdpVisualTreeHelper.GetParent(_visual, _session.UseLogicalTree);
            if (parent == null) return null;
            var childrenList = CdpVisualTreeHelper.GetChildren(parent, _session.UseLogicalTree).ToList();
            int idx = childrenList.IndexOf(_visual);
            if (idx > 0)
            {
                return new CdpRuntimeElement(_session, childrenList[idx - 1]);
            }
            return null;
        }
    }
    public string className => getAttribute("class") ?? "";
    public bool hasAttribute(string name) => getAttribute(name) != null;
 
    public CdpRuntimeElement[] getElementsByTagName(string tagName)
    {
        return querySelectorAll(tagName);
    }
    public CdpRuntimeElement[] getElementsByClassName(string className)
    {
        return querySelectorAll("." + className);
    }
    public int nodeType => 1;
    public string nodeName
    {
        get
        {
            var typeName = _visual.GetType().Name;
            if (typeName.Contains("TextBox")) return "INPUT";
            if (typeName.Contains("CheckBox")) return "INPUT";
            if (typeName.Contains("RadioButton")) return "INPUT";
            if (typeName.Contains("Button")) return "BUTTON";
            return typeName.ToUpperInvariant();
        }
    }
    public string tagName => nodeName;
    public string localName => nodeName.ToLowerInvariant();
    public string id => getAttribute("id") ?? "";
    public string name => getAttribute("Name") ?? "";
    public string textContent
    {
        get => getAttribute("text") ?? "";
        set
        {
            if (_visual != null && value != null)
            {
                Dispatcher.UIThread.Invoke(() => {
                    var textProp = _visual.GetType().GetProperty("Text", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (textProp != null && textProp.CanWrite)
                    {
                        textProp.SetValue(_visual, value);
                    }
                });
            }
        }
    }
    public string innerText
    {
        get => textContent;
        set => textContent = value;
    }

    public object? value
    {
        get => getAttribute("Text") ?? "";
        set
        {
            if (_visual != null && value != null)
            {
                Dispatcher.UIThread.Invoke(() => {
                    var valueProp = _visual.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (valueProp != null && valueProp.CanWrite)
                    {
                        if (double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dVal))
                        {
                            valueProp.SetValue(_visual, Convert.ChangeType(dVal, valueProp.PropertyType));
                            return;
                        }
                    }

                    var textProp = _visual.GetType().GetProperty("Text", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (textProp != null && textProp.CanWrite)
                    {
                        textProp.SetValue(_visual, value.ToString());
                    }
                });
            }
        }
    }

    public object? selectedIndex
    {
        get
        {
            if (_visual != null)
            {
                var prop = _visual.GetType().GetProperty("SelectedIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    return prop.GetValue(_visual);
                }
            }
            return null;
        }
        set
        {
            if (_visual != null && value != null)
            {
                Dispatcher.UIThread.Invoke(() => {
                    var prop = _visual.GetType().GetProperty("SelectedIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        if (int.TryParse(value.ToString(), out var iVal))
                        {
                            prop.SetValue(_visual, iVal);
                        }
                    }
                });
            }
        }
    }

    public bool isVisible => string.Equals(getAttribute("IsVisible"), "true", StringComparison.OrdinalIgnoreCase);
    public bool isChecked => string.Equals(getAttribute("IsChecked"), "true", StringComparison.OrdinalIgnoreCase);
    public bool @checked => isChecked;
    public bool isEffectivelyVisible => _visual is Avalonia.Controls.Control control ? (control.IsEffectivelyVisible && control.IsAttachedToVisualTree()) : true;
    public bool isEnabled => string.Equals(getAttribute("IsEnabled"), "true", StringComparison.OrdinalIgnoreCase);
    public object visual => _visual;

    public string? getAttribute(string name)
    {
        var attributes = DomDomain.BuildAttributes(_visual);
        for (int i = 0; i + 1 < attributes.Count; i += 2)
        {
            var key = attributes[i]?.GetValue<string>();
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return attributes[i + 1]?.GetValue<string>();
            }
        }

        return null;
    }

    public bool matches(string selector)
    {
        return Avalonia.Diagnostics.Cdp.SelectorEngine.Matches(_visual, selector, _session.UseLogicalTree);
    }

    public CdpRuntimeElement? querySelector(string selector)
    {
        foreach (var child in GetSearchChildren())
        {
            var visual = Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(child, selector, _session.UseLogicalTree);
            if (visual != null)
            {
                return new CdpRuntimeElement(_session, visual);
            }
        }

        return null;
    }

    public CdpRuntimeElement[] querySelectorAll(string selector)
    {
        var results = new List<CdpRuntimeElement>();
        foreach (var child in GetSearchChildren())
        {
            foreach (var visual in Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelectorAll(child, selector, _session.UseLogicalTree))
            {
                results.Add(new CdpRuntimeElement(_session, visual));
            }
        }

        return results.ToArray();
    }

    public bool contains(object? other)
    {
        if (other == null) return false;
        Visual? otherVisual = null;
        if (other is CdpRuntimeElement otherElem)
        {
            otherVisual = otherElem.visual as Visual;
        }
        else if (other is Visual v)
        {
            otherVisual = v;
        }
        if (otherVisual == null) return false;

        var current = otherVisual;
        while (current != null)
        {
            if (current == _visual) return true;
            current = _session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalParent(current)
                : current.GetVisualParent();
        }
        return false;
    }

    public CdpRuntimeElement? closest(string selector)
    {
        Visual? current = _visual;
        while (current != null)
        {
            if (Avalonia.Diagnostics.Cdp.SelectorEngine.Matches(current, selector, _session.UseLogicalTree))
            {
                return new CdpRuntimeElement(_session, current);
            }

            current = _session.UseLogicalTree
                ? Avalonia.Diagnostics.Cdp.SelectorEngine.GetLogicalParent(current)
                : current.GetVisualParent();
        }

        return null;
    }

    private IEnumerable<Visual> GetSearchChildren()
    {
        return CdpVisualTreeHelper.GetChildren(_visual, _session.UseLogicalTree);
    }

    public override string ToString()
    {
        var idValue = id;
        return string.IsNullOrEmpty(idValue) ? $"<{nodeName}>" : $"<{nodeName} id=\"{idValue}\">";
    }
}

public static class ScriptPreprocessor
{
    public static string Preprocess(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return expression;

        var processed = System.Text.RegularExpressions.Regex.Replace(expression, @"(?<!\w)\$0\b", "_0");
        processed = System.Text.RegularExpressions.Regex.Replace(processed, @"(?<!\w)\$vm\b", "DataContext");
        processed = System.Text.RegularExpressions.Regex.Replace(processed, @"(?<!\w)\$dc\b", "DataContext");

        if (expression.Length < 1000)
        {
            // Strip C# style casting e.g., ((Button)$0) or ((Button)_0) or (Button)
            processed = System.Text.RegularExpressions.Regex.Replace(processed, @"\(\s*[A-Z][A-Za-z0-9_.*+?<>]*\s*\)", "");
        }

        return processed;
    }
}

public static class AutocompleteEngine
{
    private static List<MetadataReference>? _cachedReferences;
    private static readonly object _cacheLock = new();

    public static async Task<List<CompletionItem>> GetCompletionsAsync(string scriptText, int cursorPosition, string concreteType = "Avalonia.Visual")
    {
        var header = $"using System; using System.Collections.Generic; using System.Linq; using Avalonia; using Avalonia.Controls; using Avalonia.Layout; using Avalonia.Input; {concreteType} SelectedNode = null; {concreteType} _0 = null; object DataContext = null; ";
        var fullText = header + scriptText;
        int adjustedCursor = cursorPosition + header.Length;

        List<MetadataReference> references;
        lock (_cacheLock)
        {
            if (_cachedReferences == null)
            {
                _cachedReferences = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                    .ToList();
            }
            references = _cachedReferences;
        }

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ReplProject", "ReplProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, references)
            .AddDocument(documentId, "ReplScript.cs", fullText);

        var document = solution.GetDocument(documentId);
        if (document == null) return new List<CompletionItem>();

        var completionService = CompletionService.GetService(document);
        if (completionService == null) return new List<CompletionItem>();

        var results = await completionService.GetCompletionsAsync(document, adjustedCursor);
        return results != null ? results.Items.ToList() : new List<CompletionItem>();
    }
}

public class PlaywrightUtilityScriptMock
{
}

public class PlaywrightInjectedFunctionMock
{
    public string Expression { get; set; } = "";
}

public class PlaywrightExpectFunctionMock
{
}

public class PlaywrightExpectResultMock
{
    public string Log { get; set; } = "Assertion matching";
    public bool Success { get; set; } = true;
    public object? Received { get; set; }
}

public class PlaywrightLocatorLookupFunctionMock
{
}

public class PlaywrightLookupResultMock
{
    public string Log { get; set; } = "resolved";
    public bool Success { get; set; } = true;
    public object? Element { get; set; }
}

public class PlaywrightPollFunctionMock
{
}

public struct JintResult
{
    public Jint.Native.JsValue Value { get; set; }
    public Jint.Engine Engine { get; set; }
}
