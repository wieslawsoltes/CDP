using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Avalonia.Diagnostics.Cdp.Domains;

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
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var styles = GetComputedStyles(visual);
                    return new JsonObject { ["computedStyle"] = styles };
                }

            case "getMatchedStylesForNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var inlineStyle = GetInlineStyle(visual, nodeId);
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
                    var styles = new JsonArray();
                    if (edits != null)
                    {
                        foreach (var editNode in edits)
                        {
                            if (editNode is JsonObject edit)
                            {
                                string text = edit["text"]?.GetValue<string>() ?? "";
                                // The stylesheet or node id is often tracked, we can parse it from stylesheet or fallback
                                // Let's look up the node being edited
                                // In CDP, stylesheetId matches styleSheetId, or we can look up the currently active node
                                // For simplicity, we can pass nodeId or retrieve the element
                                // Let's support editing via reflection
                                var sheetId = edit["styleSheetId"]?.GetValue<string>() ?? "";
                                if (int.TryParse(sheetId, out int nodeId))
                                {
                                    var visual = session.NodeMap.GetVisual(nodeId);
                                    if (visual is Control control)
                                    {
                                        ApplyStyleText(control, text);
                                        styles.Add(GetInlineStyle(control, nodeId));
                                    }
                                }
                            }
                        }
                    }
                    return new JsonObject { ["styles"] = styles };
                }

            default:
                throw new Exception($"Method CSS.{action} is not implemented");
        }
    }

    private static JsonArray GetComputedStyles(Visual visual)
    {
        var array = new JsonArray();
        
        // Always return width, height, and display
        array.Add(CreateCssProperty("width", $"{visual.Bounds.Width}px"));
        array.Add(CreateCssProperty("height", $"{visual.Bounds.Height}px"));
        
        if (visual is Control control)
        {
            array.Add(CreateCssProperty("display", control.IsVisible ? "block" : "none"));
            array.Add(CreateCssProperty("opacity", control.Opacity.ToString(CultureInfo.InvariantCulture)));
            array.Add(CreateCssProperty("margin", control.Margin.ToString()));

            // Try reading background
            var bgProp = control.GetType().GetProperty("Background");
            if (bgProp != null && bgProp.GetValue(control) is IBrush brush)
            {
                array.Add(CreateCssProperty("background-color", brush.ToString() ?? ""));
            }

            // Try reading padding
            var padProp = control.GetType().GetProperty("Padding");
            if (padProp != null)
            {
                array.Add(CreateCssProperty("padding", padProp.GetValue(control)?.ToString() ?? ""));
            }

            // Try reading font size/family
            var fsProp = control.GetType().GetProperty("FontSize");
            if (fsProp != null)
            {
                array.Add(CreateCssProperty("font-size", fsProp.GetValue(control)?.ToString() ?? ""));
            }
            var ffProp = control.GetType().GetProperty("FontFamily");
            if (ffProp != null)
            {
                array.Add(CreateCssProperty("font-family", ffProp.GetValue(control)?.ToString() ?? ""));
            }
        }
        else
        {
            array.Add(CreateCssProperty("display", "block"));
        }

        return array;
    }

    private static JsonObject CreateCssProperty(string name, string value)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["value"] = value
        };
    }

    private static JsonObject GetInlineStyle(Visual visual, int nodeId)
    {
        var cssProperties = new JsonArray();
        
        if (visual is Control control)
        {
            cssProperties.Add(CreateInlineProperty("width", $"{control.Width}px"));
            cssProperties.Add(CreateInlineProperty("height", $"{control.Height}px"));
            cssProperties.Add(CreateInlineProperty("opacity", control.Opacity.ToString(CultureInfo.InvariantCulture)));
            cssProperties.Add(CreateInlineProperty("margin", control.Margin.ToString()));

            var bgProp = control.GetType().GetProperty("Background");
            if (bgProp != null && bgProp.GetValue(control) is IBrush brush)
            {
                cssProperties.Add(CreateInlineProperty("background", brush.ToString() ?? ""));
            }

            var padProp = control.GetType().GetProperty("Padding");
            if (padProp != null)
            {
                cssProperties.Add(CreateInlineProperty("padding", padProp.GetValue(control)?.ToString() ?? ""));
            }
        }

        return new JsonObject
        {
            ["styleSheetId"] = nodeId.ToString(), // Map styleSheetId to nodeId for simple lookup in edits
            ["cssProperties"] = cssProperties,
            ["shorthandEntries"] = new JsonArray(),
            ["range"] = new JsonObject
            {
                ["startLine"] = 0,
                ["startColumn"] = 0,
                ["endLine"] = 0,
                ["endColumn"] = 0
            }
        };
    }

    private static JsonObject CreateInlineProperty(string name, string value)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["value"] = value,
            ["important"] = false,
            ["implicit"] = false,
            ["text"] = $"{name}: {value};",
            ["parsedOk"] = true,
            ["disabled"] = false
        };
    }

    private static void ApplyStyleText(Control control, string styleText)
    {
        var statements = styleText.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var statement in statements)
        {
            var colonIndex = statement.IndexOf(':');
            if (colonIndex == -1) continue;

            var rawName = statement.Substring(0, colonIndex).Trim();
            var rawValue = statement.Substring(colonIndex + 1).Trim();

            // Strip "px" if any for sizes
            if (rawValue.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                rawValue = rawValue.Substring(0, rawValue.Length - 2).Trim();
            }

            // Map standard CSS properties to Avalonia properties
            string propName = rawName.ToLowerInvariant() switch
            {
                "width" => "Width",
                "height" => "Height",
                "opacity" => "Opacity",
                "background" => "Background",
                "background-color" => "Background",
                "padding" => "Padding",
                "margin" => "Margin",
                "font-size" => "FontSize",
                "font-family" => "FontFamily",
                _ => rawName
            };

            SetControlProperty(control, propName, rawValue);
        }
    }

    public static bool SetControlProperty(Control control, string name, string valueStr)
    {
        // 1. Try registered Avalonia Dependency Properties
        var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(control)
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (avProperty != null)
        {
            try
            {
                var targetType = avProperty.PropertyType;
                var converted = ConvertValue(valueStr, targetType);
                control.SetValue(avProperty, converted);
                return true;
            }
            catch { }
        }

        // 2. Try Standard CLR Properties
        var clrProperty = control.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (clrProperty != null && clrProperty.CanWrite)
        {
            try
            {
                var converted = ConvertValue(valueStr, clrProperty.PropertyType);
                clrProperty.SetValue(control, converted);
                return true;
            }
            catch { }
        }

        return false;
    }

    private static object? ConvertValue(string val, Type targetType)
    {
        if (targetType == typeof(string)) return val;
        if (targetType == typeof(double)) return double.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(float)) return float.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(int)) return int.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool)) return bool.Parse(val);

        if (targetType == typeof(Thickness))
        {
            return Thickness.Parse(val);
        }

        if (typeof(IBrush).IsAssignableFrom(targetType) || targetType == typeof(Brush))
        {
            return Brush.Parse(val);
        }

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
