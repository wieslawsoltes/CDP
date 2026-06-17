using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class ApplicationDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getResources":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var array = new JsonArray();
                    if (Application.Current != null)
                    {
                        foreach (var key in Application.Current.Resources.Keys)
                        {
                            if (key == null) continue;
                            Application.Current.Resources.TryGetValue(key, out var val);
                            array.Add(new JsonObject
                            {
                                ["key"] = key.ToString(),
                                ["type"] = val?.GetType().FullName ?? "null",
                                ["value"] = val?.ToString() ?? "null"
                            });
                        }
                    }
                    return new JsonObject { ["resources"] = array };
                });

            case "setResource":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Application.Current == null) return new JsonObject();

                    string key = @params["key"]?.GetValue<string>() ?? "";
                    string valStr = @params["value"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(key)) throw new Exception("Resource key is required.");

                    object parsedValue = valStr;
                    if (Application.Current.Resources.TryGetValue(key, out var existingVal) && existingVal != null)
                    {
                        var t = existingVal.GetType();
                        if (t == typeof(double))
                        {
                            if (double.TryParse(valStr, out double d)) parsedValue = d;
                        }
                        else if (t == typeof(int))
                        {
                            if (int.TryParse(valStr, out int i)) parsedValue = i;
                        }
                        else if (t == typeof(bool))
                        {
                            if (bool.TryParse(valStr, out bool b)) parsedValue = b;
                        }
                        else if (t == typeof(Media.Color))
                        {
                            if (Media.Color.TryParse(valStr, out var color)) parsedValue = color;
                        }
                        else if (t == typeof(Media.SolidColorBrush))
                        {
                            if (Media.Color.TryParse(valStr, out var color)) parsedValue = new Media.SolidColorBrush(color);
                        }
                    }
                    else
                    {
                        if (valStr.StartsWith("#") && (valStr.Length == 7 || valStr.Length == 9))
                        {
                            if (Media.Color.TryParse(valStr, out var color))
                            {
                                parsedValue = new Media.SolidColorBrush(color);
                            }
                        }
                        else if (double.TryParse(valStr, out double d))
                        {
                            parsedValue = d;
                        }
                        else if (bool.TryParse(valStr, out bool b))
                        {
                            parsedValue = b;
                        }
                    }

                    Application.Current.Resources[key] = parsedValue;
                    return new JsonObject();
                });

            case "deleteResource":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    string key = @params["key"]?.GetValue<string>() ?? "";
                    if (Application.Current != null && !string.IsNullOrEmpty(key))
                    {
                        Application.Current.Resources.Remove(key);
                    }
                    return new JsonObject();
                });

            default:
                throw new Exception($"Method Application.{action} is not implemented");
        }
    }
}
