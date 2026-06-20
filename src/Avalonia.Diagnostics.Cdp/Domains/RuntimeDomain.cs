using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class RuntimeDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    var context = new JsonObject
                    {
                        ["id"] = 1,
                        ["origin"] = $"http://127.0.0.1:{CdpServer.Port}/",
                        ["name"] = "top",
                        ["uniqueId"] = "1"
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
                    bool returnByValue = @params["returnByValue"]?.GetValue<bool>() ?? false;

                    var result = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var val = EvaluateExpression(session, session.Window, expression);
                        return returnByValue 
                            ? new JsonObject { ["result"] = new JsonObject { ["value"] = JsonValue.Create(val) } }
                            : new JsonObject { ["result"] = CreateRemoteObject(session, val) };
                    });
                    return result;
                }

            case "callFunctionOn":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    string functionDeclaration = @params["functionDeclaration"]?.GetValue<string>() ?? "";
                    bool returnByValue = @params["returnByValue"]?.GetValue<bool>() ?? false;
                    var arguments = @params["arguments"] as JsonArray;

                    var result = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var target = session.GetObject(objectId);
                        if (target == null)
                        {
                            throw new Exception($"Object with ID {objectId} not found");
                        }

                        var val = EvaluateFunction(session, target, functionDeclaration, arguments);
                        return returnByValue
                            ? new JsonObject { ["result"] = new JsonObject { ["value"] = val == null ? null : (val is JsonNode jNode ? jNode.DeepClone() : JsonValue.Create(val)) } }
                            : new JsonObject { ["result"] = CreateRemoteObject(session, val) };
                    });
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

                    var propertiesJson = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
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
                        return list;
                    });

                    return new JsonObject { ["result"] = propertiesJson };
                }

            case "releaseObject":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
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

    private static object? EvaluateFunction(CdpSession session, object target, string functionDeclaration, JsonArray? arguments)
    {
        // 1. Parse parameters and bind to arguments
        var paramStart = functionDeclaration.IndexOf('(');
        var paramEnd = functionDeclaration.IndexOf(')');
        var variableBindings = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (paramStart != -1 && paramEnd != -1 && paramEnd > paramStart)
        {
            var paramsStr = functionDeclaration.Substring(paramStart + 1, paramEnd - paramStart - 1).Trim();
            if (!string.IsNullOrEmpty(paramsStr))
            {
                var parts = paramsStr.Split(',');
                var paramNames = new string[parts.Length];
                for (int idx = 0; idx < parts.Length; idx++)
                {
                    paramNames[idx] = parts[idx].Trim();
                }

                if (arguments != null)
                {
                    for (int i = 0; i < paramNames.Length && i < arguments.Count; i++)
                    {
                        var argObj = arguments[i] as JsonObject;
                        if (argObj != null)
                        {
                            if (argObj.TryGetPropertyValue("value", out var valNode))
                            {
                                variableBindings[paramNames[i]] = GetNodeValue(valNode);
                            }
                            else if (argObj.TryGetPropertyValue("objectId", out var objIdNode) && objIdNode != null)
                            {
                                string objId = objIdNode.GetValue<string>();
                                variableBindings[paramNames[i]] = session.GetObject(objId);
                            }
                        }
                    }
                }
            }
        }

        // 2. Extract function body between { and }
        int start = functionDeclaration.IndexOf('{');
        int end = functionDeclaration.LastIndexOf('}');
        if (start != -1 && end != -1 && end > start)
        {
            var body = functionDeclaration.Substring(start + 1, end - start - 1).Trim();
            if (body.StartsWith("return "))
            {
                body = body.Substring(7).Trim();
            }
            if (body.EndsWith(";"))
            {
                body = body.Substring(0, body.Length - 1).Trim();
            }

            // Strip "this." prefix if it exists to simplify evaluation
            if (body.StartsWith("this."))
            {
                body = body.Substring(5).Trim();
            }

            return EvaluateExpression(session, target, body, variableBindings);
        }

        return null;
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
            return trimmed.Substring(1, trimmed.Length - 2);
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

        if (trimmed.StartsWith("$0"))
        {
            var inspected = session.NodeMap.GetVisual(session.InspectedNodeId);
            if (inspected == null) throw new Exception("No inspected node ($0) is selected");

            if (trimmed == "$0") return inspected;

            var remaining = trimmed.Substring(2);
            if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
            return EvaluateExpression(session, inspected, remaining, variableBindings);
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

            var parts = propPath.Split('.');
            object current = target;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var prop = current.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) throw new Exception($"Property '{parts[i]}' not found");
                var next = prop.GetValue(current);
                if (next == null) throw new Exception($"Property '{parts[i]}' is null");
                current = next;
            }

            var lastPropName = parts[^1];
            var lastProp = current.GetType().GetProperty(lastPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (lastProp == null || !lastProp.CanWrite) throw new Exception($"Property '{lastPropName}' not found or read-only");

            object? boundValue = null;
            bool isBound = false;
            if (variableBindings != null && variableBindings.TryGetValue(valStr, out boundValue))
            {
                isBound = true;
            }

            var converted = isBound ? ConvertValueFromBound(boundValue, lastProp.PropertyType) : ConvertValue(valStr, lastProp.PropertyType);
            lastProp.SetValue(current, converted);
            return converted;
        }
        else
        {
            // Read property path: e.g. "Bounds.Width" or "Close()"
            var parts = expression.Split('.');
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
                    if (method == null)
                    {
                        // Try to find any method with name and correct number of parameters
                        var methods = current.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase)
                            .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == methodArgs.Length)
                            .ToArray();
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
                    current = method.Invoke(current, methodArgs);
                }
                else
                {
                    var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null) throw new Exception($"Property '{part}' not found on {current.GetType().Name}");
                    current = prop.GetValue(current);
                }
            }
            return current;
        }
    }

    private static JsonObject CreateRemoteObject(CdpSession session, object? obj)
    {
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

        if (obj is Visual)
        {
            result["subtype"] = "node";
        }

        return result;
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
}
