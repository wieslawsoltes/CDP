using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class CssDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "getComputedStyleForNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId) as UIElement;
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var styles = await session.Window!.DispatcherQueue.InvokeAsync(() => GetComputedStyles(visual));
                    return new JsonObject { ["computedStyle"] = styles };
                }

            case "getMatchedStylesForNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId) as UIElement;
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var inlineStyle = await session.Window!.DispatcherQueue.InvokeAsync(() => GetInlineStyle(visual, nodeId));
                    return new JsonObject
                    {
                        ["inlineStyle"] = inlineStyle,
                        ["matchedCSSRules"] = new JsonArray(),
                        ["pseudoElements"] = new JsonArray(),
                        ["inherited"] = new JsonArray(),
                        ["cssKeyframesRules"] = new JsonArray()
                    };
                }

            case "setStyleTexts":
                {
                    var edits = @params["edits"] as JsonArray;
                    var list = new JsonArray();
                    if (edits != null)
                    {
                        foreach (var editNode in edits)
                        {
                            if (editNode is JsonObject edit)
                            {
                                string text = edit["text"]?.GetValue<string>() ?? "";
                                var sheetId = edit["styleSheetId"]?.GetValue<string>() ?? "";
                                if (int.TryParse(sheetId, out int nodeId))
                                {
                                    var visual = session.NodeMap.GetVisual(nodeId);
                                    if (visual is FrameworkElement ctrl)
                                    {
                                        await ApplyStyleTextAsync(session, ctrl, text);
                                        var inlineStyle = await session.Window!.DispatcherQueue.InvokeAsync(() => GetInlineStyle(ctrl, nodeId));
                                        list.Add(inlineStyle);
                                    }
                                }
                            }
                        }
                    }
                    return new JsonObject { ["styles"] = list };
                }

            case "createStyleSheet":
                return new JsonObject { ["styleSheetId"] = "1" };

            case "setStyleSheetText":
                return new JsonObject { ["styleSheetId"] = "1" };

            default:
                throw new Exception($"Method CSS.{action} is not implemented");
        }
    }

    public static void CleanupSession(CdpSession session)
    {
    }

    private static JsonArray GetComputedStyles(UIElement visual)
    {
        var list = new JsonArray();

        void Add(string name, string val)
        {
            list.Add(new JsonObject { ["name"] = name, ["value"] = val });
        }

        Add("display", visual.Visibility == Visibility.Collapsed ? "none" : "block");

        if (visual is FrameworkElement fe)
        {
            Add("width", double.IsNaN(fe.Width) ? $"{fe.ActualWidth}px" : $"{fe.Width}px");
            Add("height", double.IsNaN(fe.Height) ? $"{fe.ActualHeight}px" : $"{fe.Height}px");
            Add("margin", fe.Margin.ToString());
            Add("horizontal-alignment", fe.HorizontalAlignment.ToString());
            Add("vertical-alignment", fe.VerticalAlignment.ToString());
        }

        var propBorder = visual.GetType().GetProperty("BorderThickness");
        if (propBorder != null)
        {
            var val = propBorder.GetValue(visual);
            if (val != null) Add("border-width", val.ToString()!);
        }

        var propPadding = visual.GetType().GetProperty("Padding");
        if (propPadding != null)
        {
            var val = propPadding.GetValue(visual);
            if (val != null) Add("padding", val.ToString()!);
        }

        var propForeground = visual.GetType().GetProperty("Foreground");
        if (propForeground != null)
        {
            var val = propForeground.GetValue(visual);
            if (val != null) Add("color", val.ToString()!);
        }

        var propBackground = visual.GetType().GetProperty("Background");
        if (propBackground != null)
        {
            var val = propBackground.GetValue(visual);
            if (val != null) Add("background-color", val.ToString()!);
        }

        var propFontFamily = visual.GetType().GetProperty("FontFamily");
        if (propFontFamily != null)
        {
            var val = propFontFamily.GetValue(visual);
            if (val != null) Add("font-family", val.ToString()!);
        }

        var propFontSize = visual.GetType().GetProperty("FontSize");
        if (propFontSize != null)
        {
            var val = propFontSize.GetValue(visual);
            if (val != null) Add("font-size", $"{val}px");
        }

        return list;
    }

    private static JsonObject GetInlineStyle(DependencyObject visual, int nodeId)
    {
        var cssProperties = new JsonArray();

        // Scan dependency properties using static fields
        var fields = visual.GetType().GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(DependencyProperty) && field.Name.EndsWith("Property"))
            {
                try
                {
                    if (field.GetValue(null) is DependencyProperty dp)
                    {
                        var val = visual.ReadLocalValue(dp);
                        if (val != DependencyProperty.UnsetValue && val != null)
                        {
                            var propName = field.Name.Substring(0, field.Name.Length - 8).ToLowerInvariant();
                            cssProperties.Add(new JsonObject
                            {
                                ["name"] = propName,
                                ["value"] = val.ToString(),
                                ["important"] = false,
                                ["implicit"] = false,
                                ["text"] = $"{propName}: {val}",
                                ["parsedOk"] = true,
                                ["disabled"] = false
                            });
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return new JsonObject
        {
            ["styleSheetId"] = nodeId.ToString(),
            ["cssProperties"] = cssProperties,
            ["shorthandEntries"] = new JsonArray(),
            ["cssText"] = ""
        };
    }

    private static async Task ApplyStyleTextAsync(CdpSession session, FrameworkElement element, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // 1. Run live UI update on dispatcher thread
        await session.Window!.DispatcherQueue.InvokeAsync(() =>
        {
            ApplyStyleLive(element, text);
        });

        // 2. Run AST/file mutation on background/current thread
        if (session.MutationEngine != null && session.MutationEngine.CanMutate(element))
        {
            var decls = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var decl in decls)
            {
                int colonIndex = decl.IndexOf(':');
                if (colonIndex < 0) continue;

                string name = decl.Substring(0, colonIndex).Trim();
                string valStr = decl.Substring(colonIndex + 1).Trim();

                string propName = name switch
                {
                    "width" => "Width",
                    "height" => "Height",
                    "opacity" => "Opacity",
                    "background" => "Background",
                    "background-color" => "Background",
                    "font-size" => "FontSize",
                    "font-family" => "FontFamily",
                    _ => name
                };

                await session.MutationEngine.SetAttributeAsync(element, propName, valStr);
            }
        }
    }

    private static void ApplyStyleLive(FrameworkElement element, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var decls = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var decl in decls)
        {
            int colonIndex = decl.IndexOf(':');
            if (colonIndex < 0) continue;

            string name = decl.Substring(0, colonIndex).Trim();
            string valStr = decl.Substring(colonIndex + 1).Trim();

            var prop = element.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    object? value = null;
                    if (prop.PropertyType == typeof(string))
                    {
                        value = valStr;
                    }
                    else if (prop.PropertyType == typeof(double))
                    {
                        value = double.Parse(valStr.Replace("px", "").Trim());
                    }
                    else if (prop.PropertyType == typeof(float))
                    {
                        value = float.Parse(valStr.Replace("px", "").Trim());
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        value = int.Parse(valStr);
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        value = Enum.Parse(prop.PropertyType, valStr, true);
                    }
                    else if (prop.PropertyType == typeof(Thickness))
                    {
                        var parts = valStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 1)
                        {
                            value = new Thickness(double.Parse(parts[0]));
                        }
                        else if (parts.Length == 4)
                        {
                            value = new Thickness(double.Parse(parts[0]), double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]));
                        }
                    }

                    if (value != null)
                    {
                        prop.SetValue(element, value);
                    }
                }
                catch { }
            }
        }
    }
}
