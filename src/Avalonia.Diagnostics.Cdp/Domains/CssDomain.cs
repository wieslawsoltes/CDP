using System;
using System.Collections.Concurrent;
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
using Avalonia.Threading;

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
                    var styles = await Dispatcher.UIThread.InvokeAsync(() => GetComputedStyles(visual));
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
                    var inlineStyle = await Dispatcher.UIThread.InvokeAsync(() => GetInlineStyle(visual, nodeId));
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
                    var styles = await Dispatcher.UIThread.InvokeAsync(() =>
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
                                        if (visual is Control control)
                                        {
                                            ApplyStyleText(control, text);
                                            list.Add(GetInlineStyle(control, nodeId));
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
                {
                    return new JsonObject { ["styleSheetId"] = "1" };
                }

            case "setStyleSheetText":
                {
                    string sheetId = @params["styleSheetId"]?.GetValue<string>() ?? "";
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (int.TryParse(sheetId, out int nodeId))
                        {
                            var visual = session.NodeMap.GetVisual(nodeId);
                            if (visual is Control control)
                            {
                                ApplyStyleText(control, text);
                            }
                        }
                    });
                    return new JsonObject { ["sourceMapURL"] = "" };
                }

            case "getInlineStylesForNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var inlineStyle = await Dispatcher.UIThread.InvokeAsync(() => GetInlineStyle(visual, nodeId));
                    return new JsonObject
                    {
                        ["inlineStyle"] = inlineStyle,
                        ["attributesStyle"] = new JsonObject
                        {
                            ["styleSheetId"] = nodeId.ToString(),
                            ["cssProperties"] = new JsonArray(),
                            ["shorthandEntries"] = new JsonArray(),
                            ["cssText"] = ""
                        }
                    };
                }

            case "forcePseudoState":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var classes = @params["forcedPseudoClasses"] as JsonArray;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual is Control control)
                        {
                            var pseudoProp = control.GetType().GetProperty("PseudoClasses", 
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (pseudoProp != null)
                            {
                                var pseudoClasses = pseudoProp.GetValue(control);
                                if (pseudoClasses != null)
                                {
                                    var removeMethod = pseudoClasses.GetType().GetMethod("Remove", new[] { typeof(string) });
                                    var addMethod = pseudoClasses.GetType().GetMethod("Add", new[] { typeof(string) });
                                    if (removeMethod != null && addMethod != null)
                                    {
                                        removeMethod.Invoke(pseudoClasses, new object[] { "hover" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "pointerover" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "active" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "pressed" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "focus" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "focus-within" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "focus-visible" });
                                        removeMethod.Invoke(pseudoClasses, new object[] { "disabled" });

                                        if (classes != null)
                                        {
                                            foreach (var clsNode in classes)
                                            {
                                                string cls = clsNode?.GetValue<string>() ?? "";
                                                if (string.Equals(cls, "hover", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    addMethod.Invoke(pseudoClasses, new object[] { "hover" });
                                                    addMethod.Invoke(pseudoClasses, new object[] { "pointerover" });
                                                }
                                                else if (string.Equals(cls, "active", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    addMethod.Invoke(pseudoClasses, new object[] { "active" });
                                                    addMethod.Invoke(pseudoClasses, new object[] { "pressed" });
                                                }
                                                else if (!string.IsNullOrEmpty(cls))
                                                {
                                                    // Strip colon if passed
                                                    if (cls.StartsWith(":")) cls = cls.Substring(1);
                                                    addMethod.Invoke(pseudoClasses, new object[] { cls });
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });
                    return new JsonObject();
                }

            case "getPlatformFontsForNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var fonts = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
                        string family = "Default";
                        if (visual is Control control)
                        {
                            var ffProp = control.GetType().GetProperty("FontFamily");
                            if (ffProp != null && ffProp.GetValue(control) is FontFamily ff)
                            {
                                family = ff.Name;
                            }
                        }
                        list.Add(new JsonObject
                        {
                            ["familyName"] = family,
                            ["postScriptName"] = family,
                            ["isCustomFont"] = false,
                            ["glyphCount"] = 1
                        });
                        return list;
                    });
                    return new JsonObject { ["fonts"] = fonts };
                }

            case "getBackgroundColors":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var result = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var res = new JsonObject();
                        var colors = new JsonArray();
                        string fontSize = "12px";
                        string fontWeight = "normal";

                        if (visual is Control control)
                        {
                            var bgProp = control.GetType().GetProperty("Background");
                            if (bgProp != null && bgProp.GetValue(control) is IBrush brush)
                            {
                                colors.Add(brush.ToString() ?? "");
                            }
                            else
                            {
                                colors.Add("#00000000");
                            }

                            var fsProp = control.GetType().GetProperty("FontSize");
                            if (fsProp != null)
                            {
                                fontSize = $"{fsProp.GetValue(control)}px";
                            }
                            var fwProp = control.GetType().GetProperty("FontWeight");
                            if (fwProp != null)
                            {
                                fontWeight = fwProp.GetValue(control)?.ToString() ?? "normal";
                            }
                        }
                        else
                        {
                            colors.Add("#00000000");
                        }

                        res["backgroundColors"] = colors;
                        res["computedFontSize"] = fontSize;
                        res["computedFontWeight"] = fontWeight;
                        return res;
                    });
                    return result;
                }

            case "collectClassNames":
                {
                    string sheetId = @params["styleSheetId"]?.GetValue<string>() ?? "";
                    var classNames = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var list = new JsonArray();
                        if (int.TryParse(sheetId, out int nodeId))
                        {
                            var visual = session.NodeMap.GetVisual(nodeId);
                            if (visual is Control control)
                            {
                                foreach (var cls in control.Classes)
                                {
                                    list.Add(cls);
                                }
                                var pseudoProp = control.GetType().GetProperty("PseudoClasses", 
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (pseudoProp != null && pseudoProp.GetValue(control) is System.Collections.IEnumerable pseudoClasses)
                                {
                                    foreach (var pc in pseudoClasses)
                                    {
                                        if (pc != null)
                                        {
                                            string pcStr = pc.ToString() ?? "";
                                            if (!pcStr.StartsWith(":"))
                                            {
                                                pcStr = ":" + pcStr;
                                            }
                                            list.Add(pcStr);
                                        }
                                    }
                                }
                            }
                        }
                        return list;
                    });
                    return new JsonObject { ["classNames"] = classNames };
                }

            case "getStyleSheetText":
                {
                    string sheetId = @params["styleSheetId"]?.GetValue<string>() ?? "";
                    var text = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (int.TryParse(sheetId, out int nodeId))
                        {
                            var visual = session.NodeMap.GetVisual(nodeId);
                            if (visual != null)
                            {
                                var styleObj = GetInlineStyle(visual, nodeId);
                                return styleObj["cssText"]?.GetValue<string>() ?? "";
                            }
                        }
                        return "";
                    });
                    return new JsonObject { ["text"] = text };
                }

            case "addRule":
                {
                    string sheetId = @params["styleSheetId"]?.GetValue<string>() ?? "";
                    string ruleText = @params["ruleText"]?.GetValue<string>() ?? "";
                    var rule = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (int.TryParse(sheetId, out int nodeId))
                        {
                            var visual = session.NodeMap.GetVisual(nodeId);
                            if (visual is Control control)
                            {
                                ApplyStyleText(control, ruleText);
                                var inlineStyle = GetInlineStyle(control, nodeId);
                                return new JsonObject
                                {
                                    ["styleSheetId"] = sheetId,
                                    ["selectorList"] = new JsonObject
                                    {
                                        ["selectors"] = new JsonArray { new JsonObject { ["text"] = "element" } },
                                        ["text"] = "element"
                                    },
                                    ["origin"] = "inspector",
                                    ["style"] = inlineStyle
                                };
                            }
                        }
                        return new JsonObject();
                    });
                    return new JsonObject { ["rule"] = rule };
                }

            case "getLayersForNode":
                {
                    return new JsonObject
                    {
                        ["rootLayer"] = new JsonObject
                        {
                            ["name"] = "default",
                            ["order"] = 1
                        }
                    };
                }

            case "getMediaQueries":
                {
                    return new JsonObject
                    {
                        ["medias"] = new JsonArray()
                    };
                }

            case "getLonghandProperties":
                {
                    string shorthandName = @params["shorthandName"]?.GetValue<string>() ?? "";
                    string value = @params["value"]?.GetValue<string>() ?? "";
                    var list = new JsonArray();
                    
                    var parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string t = "0", r = "0", b = "0", l = "0";
                    if (parts.Length == 1)
                    {
                        t = r = b = l = parts[0];
                    }
                    else if (parts.Length == 2)
                    {
                        t = b = parts[0];
                        r = l = parts[1];
                    }
                    else if (parts.Length == 3)
                    {
                        t = parts[0];
                        r = l = parts[1];
                        b = parts[2];
                    }
                    else if (parts.Length == 4)
                    {
                        t = parts[0];
                        r = parts[1];
                        b = parts[2];
                        l = parts[3];
                    }

                    if (shorthandName.Equals("margin", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(CreateInlineProperty("margin-top", t));
                        list.Add(CreateInlineProperty("margin-right", r));
                        list.Add(CreateInlineProperty("margin-bottom", b));
                        list.Add(CreateInlineProperty("margin-left", l));
                    }
                    else if (shorthandName.Equals("padding", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(CreateInlineProperty("padding-top", t));
                        list.Add(CreateInlineProperty("padding-right", r));
                        list.Add(CreateInlineProperty("padding-bottom", b));
                        list.Add(CreateInlineProperty("padding-left", l));
                    }
                    else
                    {
                        list.Add(CreateInlineProperty(shorthandName, value));
                    }

                    return new JsonObject { ["longhandProperties"] = list };
                }

            case "getLocationForSelector":
                {
                    string sheetId = @params["styleSheetId"]?.GetValue<string>() ?? "";
                    string selectorText = @params["selectorText"]?.GetValue<string>() ?? "";
                    var ranges = new JsonArray();

                    if (int.TryParse(sheetId, out int nodeId))
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual != null)
                        {
                            if (selectorText.Equals("element", StringComparison.OrdinalIgnoreCase))
                            {
                                ranges.Add(new JsonObject
                                {
                                    ["startLine"] = 0,
                                    ["startColumn"] = 0,
                                    ["endLine"] = 0,
                                    ["endColumn"] = selectorText.Length
                                });
                            }
                        }
                    }
                    return new JsonObject { ["ranges"] = ranges };
                }

            case "getAnimatedStylesForNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var transitionsStyle = new JsonObject();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual is Control control)
                        {
                            transitionsStyle = GetTransitionsStyle(control, nodeId);
                        }
                    });
                    return new JsonObject
                    {
                        ["animationStyles"] = new JsonArray(),
                        ["transitionsStyle"] = transitionsStyle,
                        ["inherited"] = new JsonArray()
                    };
                }

            case "getEnvironmentVariables":
                {
                    double scaling = 1.0;
                    var windows = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.Windows
                        : Array.Empty<Window>();
                    if (windows.Count > 0)
                    {
                        scaling = windows[0].RenderScaling;
                    }

                    return new JsonObject
                    {
                        ["environmentVariables"] = new JsonObject
                        {
                            ["device-pixel-ratio"] = scaling.ToString("0.##", CultureInfo.InvariantCulture),
                            ["safe-area-inset-top"] = "0px",
                            ["safe-area-inset-right"] = "0px",
                            ["safe-area-inset-bottom"] = "0px",
                            ["safe-area-inset-left"] = "0px"
                        }
                    };
                }

            case "resolveValues":
                {
                    var values = @params["values"] as JsonArray;
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var results = new JsonArray();
                    double fontSize = 16.0;

                    if (nodeId > 0)
                    {
                        fontSize = await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var visual = session.NodeMap.GetVisual(nodeId);
                            if (visual is Control control)
                            {
                                var fsProp = control.GetType().GetProperty("FontSize");
                                if (fsProp != null && fsProp.GetValue(control) is double fsDouble)
                                {
                                    return fsDouble;
                                }
                            }
                            return 16.0;
                        });
                    }

                    if (values != null)
                    {
                        foreach (var valNode in values)
                        {
                            string val = valNode?.GetValue<string>() ?? "";
                            string resolvedVal = ResolveCssValue(val, fontSize);
                            results.Add(resolvedVal);
                        }
                    }
                    return new JsonObject { ["results"] = results };
                }

            case "trackComputedStyleUpdates":
                {
                    var props = @params["propertiesToTrack"] as JsonArray;
                    if (props != null && props.Count > 0)
                    {
                        var set = _sessionTrackedComputedNodes.GetOrAdd(session, _ => new HashSet<int>());
                        lock (set)
                        {
                            set.Add(-1); // track globally
                        }
                    }
                    else
                    {
                        _sessionTrackedComputedNodes.TryRemove(session, out _);
                        _sessionUpdatedComputedStyleNodes.TryRemove(session, out _);
                    }
                    return new JsonObject();
                }

            case "trackComputedStyleUpdatesForNode":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    if (nodeId.HasValue && nodeId.Value > 0)
                    {
                        _sessionSingleTrackedNode[session] = nodeId.Value;
                    }
                    else
                    {
                        _sessionSingleTrackedNode.TryRemove(session, out _);
                    }
                    return new JsonObject();
                }

            case "takeComputedStyleUpdates":
                {
                    var nodeIds = new JsonArray();
                    if (_sessionUpdatedComputedStyleNodes.TryRemove(session, out var updatedDict))
                    {
                        foreach (var key in updatedDict.Keys)
                        {
                            nodeIds.Add(key);
                        }
                    }
                    return new JsonObject { ["nodeIds"] = nodeIds };
                }

            case "startRuleUsageTracking":
            case "forceStartingStyle":
            case "setLocalFontsEnabled":
            case "setContainerQueryConditionText":
            case "setContainerQueryText":
            case "setEffectivePropertyValueForNode":
            case "setKeyframeKey":
            case "setMediaText":
            case "setNavigationText":
            case "setPropertyRulePropertyName":
            case "setRuleSelector":
            case "setScopeText":
            case "setSupportsText":
                {
                    return new JsonObject();
                }

            case "stopRuleUsageTracking":
                {
                    return new JsonObject
                    {
                        ["ruleUsage"] = new JsonArray()
                    };
                }

            case "takeCoverageDelta":
                {
                    return new JsonObject
                    {
                        ["coverage"] = new JsonArray(),
                        ["timestamp"] = 0.0
                    };
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

        var cssText = string.Join(" ", cssProperties.Cast<JsonObject>().Select(p => p["text"]?.GetValue<string>() ?? ""));
        return new JsonObject
        {
            ["styleSheetId"] = nodeId.ToString(), // Map styleSheetId to nodeId for simple lookup in edits
            ["cssProperties"] = cssProperties,
            ["shorthandEntries"] = new JsonArray(),
            ["cssText"] = cssText,
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

    private static readonly ConcurrentDictionary<CdpSession, HashSet<int>> _sessionTrackedComputedNodes = new();
    private static readonly ConcurrentDictionary<CdpSession, ConcurrentDictionary<int, bool>> _sessionUpdatedComputedStyleNodes = new();
    private static readonly ConcurrentDictionary<CdpSession, int> _sessionSingleTrackedNode = new();

    public static void OnPropertyChanged(CdpSession session, int nodeId, string propertyName)
    {
        // 1. Check global/multiple tracking
        if (_sessionTrackedComputedNodes.TryGetValue(session, out var trackedSet))
        {
            lock (trackedSet)
            {
                if (trackedSet.Contains(-1) || trackedSet.Contains(nodeId))
                {
                    var updatedDict = _sessionUpdatedComputedStyleNodes.GetOrAdd(session, _ => new ConcurrentDictionary<int, bool>());
                    updatedDict[nodeId] = true;
                }
            }
        }

        // 2. Check single node tracking for events
        if (_sessionSingleTrackedNode.TryGetValue(session, out int singleTrackedNodeId))
        {
            if (singleTrackedNodeId == nodeId)
            {
                // Send CSS.computedStyleUpdated event
                _ = session.SendEventAsync("CSS.computedStyleUpdated", new JsonObject
                {
                    ["nodeId"] = nodeId
                });
            }
        }
    }

    public static void CleanupSession(CdpSession session)
    {
        _sessionTrackedComputedNodes.TryRemove(session, out _);
        _sessionUpdatedComputedStyleNodes.TryRemove(session, out _);
        _sessionSingleTrackedNode.TryRemove(session, out _);
    }

    private static string ResolveCssValue(string val, double fontSize)
    {
        val = val.Trim();
        if (val.EndsWith("em", StringComparison.OrdinalIgnoreCase) && 
            double.TryParse(val.Substring(0, val.Length - 2).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double emCount))
        {
            return $"{(emCount * fontSize).ToString("0.##", CultureInfo.InvariantCulture)}px";
        }
        if (val.StartsWith("calc(", StringComparison.OrdinalIgnoreCase) && val.EndsWith(")"))
        {
            string expression = val.Substring(5, val.Length - 6).Trim();
            try
            {
                var parts = expression.Split(new[] { '+', '-', '*', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    string p1 = parts[0].Trim();
                    string p2 = parts[1].Trim();
                    double v1 = ParseUnitValue(p1, fontSize);
                    double v2 = ParseUnitValue(p2, fontSize);
                    char op = ' ';
                    foreach (var c in expression)
                    {
                        if (c == '+' || c == '-' || c == '*' || c == '/')
                        {
                            op = c;
                            break;
                        }
                    }
                    double result = op switch
                    {
                        '+' => v1 + v2,
                        '-' => v1 - v2,
                        '*' => v1 * v2,
                        '/' => v2 != 0 ? v1 / v2 : 0,
                        _ => 0
                    };
                    return $"{result.ToString("0.##", CultureInfo.InvariantCulture)}px";
                }
            }
            catch { }
        }
        return val;
    }

    private static double ParseUnitValue(string val, double fontSize)
    {
        val = val.Trim();
        if (val.EndsWith("px", StringComparison.OrdinalIgnoreCase) && 
            double.TryParse(val.Substring(0, val.Length - 2).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double pxVal))
        {
            return pxVal;
        }
        if (val.EndsWith("em", StringComparison.OrdinalIgnoreCase) && 
            double.TryParse(val.Substring(0, val.Length - 2).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double emVal))
        {
            return emVal * fontSize;
        }
        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleVal))
        {
            return doubleVal;
        }
        return 0;
    }

    private static JsonObject GetTransitionsStyle(Control control, int nodeId)
    {
        var cssProperties = new JsonArray();
        var sbText = new System.Text.StringBuilder();

        var transProp = control.GetType().GetProperty("Transitions");
        if (transProp != null && transProp.GetValue(control) is System.Collections.IEnumerable transitions)
        {
            var propNames = new System.Collections.Generic.List<string>();
            var durStrings = new System.Collections.Generic.List<string>();

            foreach (var t in transitions)
            {
                if (t == null) continue;
                var propOfTrans = t.GetType().GetProperty("Property");
                var durOfTrans = t.GetType().GetProperty("Duration");
                if (propOfTrans != null && durOfTrans != null)
                {
                    var propVal = propOfTrans.GetValue(t);
                    var durVal = durOfTrans.GetValue(t);

                    string propName = propVal?.ToString() ?? "all";
                    if (propName.Contains("Property")) propName = propName.Replace("Property", "").ToLower();

                    var durSpan = durVal is TimeSpan ts ? ts : TimeSpan.Zero;
                    string durStr = $"{durSpan.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";

                    propNames.Add(propName);
                    durStrings.Add(durStr);
                }
            }

            if (propNames.Count > 0)
            {
                string combinedProps = string.Join(", ", propNames);
                string combinedDurs = string.Join(", ", durStrings);

                cssProperties.Add(CreateInlineProperty("transition-property", combinedProps));
                cssProperties.Add(CreateInlineProperty("transition-duration", combinedDurs));
                sbText.Append($"transition-property: {combinedProps}; transition-duration: {combinedDurs};");
            }
        }

        return new JsonObject
        {
            ["styleSheetId"] = nodeId.ToString(),
            ["cssProperties"] = cssProperties,
            ["shorthandEntries"] = new JsonArray(),
            ["cssText"] = sbText.ToString()
        };
    }
}
