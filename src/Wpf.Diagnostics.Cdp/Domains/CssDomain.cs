using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp.Domains;

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
                    var styles = await session.Window!.Dispatcher.InvokeAsync(() => GetComputedStyles(visual));
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
                    var inlineStyle = await session.Window!.Dispatcher.InvokeAsync(() => GetInlineStyle(visual, nodeId));
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
                    var styles = await session.Window!.Dispatcher.InvokeAsync(() =>
                    {
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
                                            ApplyStyleText(ctrl, text);
                                            list.Add(GetInlineStyle(ctrl, nodeId));
                                        }
                                    }
                                }
                            }
                        }
                        return list;
                    });
                    return new JsonObject { ["styles"] = styles };
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

    private static JsonArray GetComputedStyles(Visual visual)
    {
        var list = new JsonArray();

        void Add(string name, string val)
        {
            list.Add(new JsonObject { ["name"] = name, ["value"] = val });
        }

        Add("display", visual is UIElement ui && ui.Visibility == Visibility.Collapsed ? "none" : "block");

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

    private static JsonObject GetInlineStyle(Visual visual, int nodeId)
    {
        var cssProperties = new JsonArray();

        if (visual is FrameworkElement fe)
        {
            var properties = TypeDescriptor.GetProperties(fe);
            foreach (PropertyDescriptor prop in properties)
            {
                var dpd = DependencyPropertyDescriptor.FromProperty(prop);
                if (dpd != null)
                {
                    // Check if local value is set (i.e. inline style equivalent)
                    var val = fe.ReadLocalValue(dpd.DependencyProperty);
                    if (val != DependencyProperty.UnsetValue && val != null)
                    {
                        var propName = prop.Name.ToLowerInvariant();
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
        }

        return new JsonObject
        {
            ["styleSheetId"] = nodeId.ToString(),
            ["cssProperties"] = cssProperties,
            ["shorthandEntries"] = new JsonArray(),
            ["cssText"] = ""
        };
    }

    private static void ApplyStyleText(FrameworkElement element, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var decls = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var decl in decls)
        {
            int colonIndex = decl.IndexOf(':');
            if (colonIndex < 0) continue;

            string name = decl.Substring(0, colonIndex).Trim();
            string valStr = decl.Substring(colonIndex + 1).Trim();

            var propDesc = TypeDescriptor.GetProperties(element)
                .Cast<PropertyDescriptor>()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (propDesc != null)
            {
                try
                {
                    var converter = propDesc.Converter;
                    if (converter != null && converter.CanConvertFrom(typeof(string)))
                    {
                        var typedVal = converter.ConvertFromString(null, CultureInfo.InvariantCulture, valStr);
                        propDesc.SetValue(element, typedVal);
                    }
                }
                catch { }
            }
        }
    }
}
