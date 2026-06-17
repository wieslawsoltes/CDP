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

                    var result = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var target = session.GetObject(objectId);
                        if (target == null)
                        {
                            throw new Exception($"Object with ID {objectId} not found");
                        }

                        var val = EvaluateFunction(session, target, functionDeclaration);
                        return returnByValue
                            ? new JsonObject { ["result"] = new JsonObject { ["value"] = JsonValue.Create(val) } }
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
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Runtime.{action} is not implemented");
        }
    }

    private static object? EvaluateFunction(CdpSession session, object target, string functionDeclaration)
    {
        // Extract function body between { and }
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

            return EvaluateExpression(session, target, body);
        }

        return null;
    }

    private static object? EvaluateExpression(CdpSession session, object target, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;

        if (expression.StartsWith("$0"))
        {
            var inspected = session.NodeMap.GetVisual(session.InspectedNodeId);
            if (inspected == null) throw new Exception("No inspected node ($0) is selected");

            if (expression == "$0") return inspected;

            var remaining = expression.Substring(2);
            if (remaining.StartsWith(".")) remaining = remaining.Substring(1);
            return EvaluateExpression(session, inspected, remaining);
        }

        // Strip leading "this." if present
        if (expression.StartsWith("this."))
        {
            expression = expression.Substring(5).Trim();
        }

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

            var converted = ConvertValue(valStr, lastProp.PropertyType);
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

                    // Resolve arguments
                    object?[] methodArgs;
                    Type[] argTypes;

                    if (string.IsNullOrEmpty(argStr))
                    {
                        methodArgs = Array.Empty<object?>();
                        argTypes = Type.EmptyTypes;
                    }
                    else
                    {
                        // Clean quotes if it is a string literal
                        if ((argStr.StartsWith("\"") && argStr.EndsWith("\"")) || (argStr.StartsWith("'") && argStr.EndsWith("'")))
                        {
                            var unescaped = argStr.Substring(1, argStr.Length - 2)
                                .Replace("\\\"", "\"")
                                .Replace("\\n", "\n")
                                .Replace("\\r", "\r")
                                .Replace("\\t", "\t");
                            methodArgs = new object?[] { unescaped };
                            argTypes = new Type[] { typeof(string) };
                        }
                        else if (int.TryParse(argStr, out int intVal))
                        {
                            methodArgs = new object?[] { intVal };
                            argTypes = new Type[] { typeof(int) };
                        }
                        else if (double.TryParse(argStr, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleVal))
                        {
                            methodArgs = new object?[] { doubleVal };
                            argTypes = new Type[] { typeof(double) };
                        }
                        else if (bool.TryParse(argStr, out bool boolVal))
                        {
                            methodArgs = new object?[] { boolVal };
                            argTypes = new Type[] { typeof(bool) };
                        }
                        else
                        {
                            // Fallback as raw string
                            methodArgs = new object?[] { argStr };
                            argTypes = new Type[] { typeof(string) };
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
}
