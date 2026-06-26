using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;

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
                    var matchedRules = await Dispatcher.UIThread.InvokeAsync(() => GetMatchedRules(visual));
                    return new JsonObject
                    {
                        ["inlineStyle"] = inlineStyle,
                        ["matchedCSSRules"] = matchedRules,
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
                            var pseudoProp = typeof(Control).GetProperty("PseudoClasses", 
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (pseudoProp != null && pseudoProp.GetValue(control) is IPseudoClasses pseudoClasses)
                            {
                                pseudoClasses.Remove("hover");
                                pseudoClasses.Remove("pointerover");
                                pseudoClasses.Remove("active");
                                pseudoClasses.Remove("pressed");
                                pseudoClasses.Remove("focus");
                                pseudoClasses.Remove("focus-within");
                                pseudoClasses.Remove("focus-visible");
                                pseudoClasses.Remove("disabled");

                                if (classes != null)
                                {
                                    foreach (var clsNode in classes)
                                    {
                                        string cls = clsNode?.GetValue<string>() ?? "";
                                        if (string.Equals(cls, "hover", StringComparison.OrdinalIgnoreCase))
                                        {
                                            pseudoClasses.Add("hover");
                                            pseudoClasses.Add("pointerover");
                                        }
                                        else if (string.Equals(cls, "active", StringComparison.OrdinalIgnoreCase))
                                        {
                                            pseudoClasses.Add("active");
                                            pseudoClasses.Add("pressed");
                                        }
                                        else if (!string.IsNullOrEmpty(cls))
                                        {
                                            // Strip colon if passed
                                            if (cls.StartsWith(":")) cls = cls.Substring(1);
                                            pseudoClasses.Add(cls);
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
                            if (GetControlProperty(control, "FontFamily") is FontFamily ff)
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
                            if (GetControlProperty(control, "Background") is IBrush brush)
                            {
                                colors.Add(brush.ToString() ?? "");
                            }
                            else
                            {
                                colors.Add("#00000000");
                            }

                            if (GetControlProperty(control, "FontSize") is double fs)
                            {
                                fontSize = $"{fs}px";
                            }
                            else if (GetControlProperty(control, "FontSize") is object fsObj)
                            {
                                fontSize = $"{fsObj}px";
                            }

                            if (GetControlProperty(control, "FontWeight") is FontWeight fw)
                            {
                                fontWeight = fw.ToString();
                            }
                            else if (GetControlProperty(control, "FontWeight") is object fwObj)
                            {
                                fontWeight = fwObj.ToString() ?? "normal";
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
                                var pseudoProp = typeof(Control).GetProperty("PseudoClasses", 
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
                                if (GetControlProperty(control, "FontSize") is double fsDouble)
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
            if (GetControlProperty(control, "Background") is IBrush brush)
            {
                array.Add(CreateCssProperty("background-color", brush.ToString() ?? ""));
            }

            // Try reading padding
            if (GetControlProperty(control, "Padding") is object paddingVal)
            {
                array.Add(CreateCssProperty("padding", paddingVal.ToString() ?? ""));
            }

            // Try reading font size/family
            if (GetControlProperty(control, "FontSize") is object fontSizeVal)
            {
                array.Add(CreateCssProperty("font-size", fontSizeVal.ToString() ?? ""));
            }
            if (GetControlProperty(control, "FontFamily") is object fontFamilyVal)
            {
                array.Add(CreateCssProperty("font-family", fontFamilyVal.ToString() ?? ""));
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

            if (GetControlProperty(control, "Background") is IBrush brush)
            {
                cssProperties.Add(CreateInlineProperty("background", brush.ToString() ?? ""));
            }

            if (GetControlProperty(control, "Padding") is object paddingVal)
            {
                cssProperties.Add(CreateInlineProperty("padding", paddingVal.ToString() ?? ""));
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

    private static Thickness ParseCssThickness(string value)
    {
        var parts = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var vals = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (p.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                p = p.Substring(0, p.Length - 2).Trim();
            }
            vals[i] = double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0.0;
        }

        if (vals.Length == 1)
        {
            return new Thickness(vals[0]);
        }
        if (vals.Length == 2)
        {
            double v = vals[0];
            double h = vals[1];
            return new Thickness(h, v, h, v);
        }
        if (vals.Length == 3)
        {
            double t = vals[0];
            double h = vals[1];
            double b = vals[2];
            return new Thickness(h, t, h, b);
        }
        if (vals.Length >= 4)
        {
            double t = vals[0];
            double r = vals[1];
            double b = vals[2];
            double l = vals[3];
            return new Thickness(l, t, r, b);
        }
        return new Thickness();
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

            // Strip "px" if any for sizes (if it's not a shorthand with multiple px values)
            string normalizedValue = rawValue;
            if (normalizedValue.EndsWith("px", StringComparison.OrdinalIgnoreCase) && !normalizedValue.Contains(" ") && !normalizedValue.Contains(","))
            {
                normalizedValue = normalizedValue.Substring(0, normalizedValue.Length - 2).Trim();
            }

            string lowerName = rawName.ToLowerInvariant();
            
            // Check if it is a shorthand or longhand thickness property
            string basePropName = "";
            string component = "";
            bool isThickness = false;

            if (lowerName == "margin")
            {
                basePropName = "Margin";
                isThickness = true;
            }
            else if (lowerName.StartsWith("margin-", StringComparison.OrdinalIgnoreCase))
            {
                basePropName = "Margin";
                component = lowerName.Substring("margin-".Length);
                isThickness = true;
            }
            else if (lowerName == "padding")
            {
                basePropName = "Padding";
                isThickness = true;
            }
            else if (lowerName.StartsWith("padding-", StringComparison.OrdinalIgnoreCase))
            {
                basePropName = "Padding";
                component = lowerName.Substring("padding-".Length);
                isThickness = true;
            }
            else if (lowerName == "border-thickness" || lowerName == "border-width" || lowerName == "border")
            {
                basePropName = "BorderThickness";
                isThickness = true;
            }
            else if (lowerName.StartsWith("border-", StringComparison.OrdinalIgnoreCase))
            {
                basePropName = "BorderThickness";
                var rest = lowerName.Substring("border-".Length);
                if (rest.EndsWith("-width"))
                {
                    component = rest.Substring(0, rest.Length - "-width".Length);
                }
                else
                {
                    component = rest;
                }
                isThickness = true;
            }

            if (isThickness)
            {
                Thickness currentThickness = new Thickness();
                var currentVal = GetControlProperty(control, basePropName);
                if (currentVal is Thickness t)
                {
                    currentThickness = t;
                }

                if (string.IsNullOrEmpty(component))
                {
                    // Shorthand margin/padding/border
                    Thickness parsedThickness = ParseCssThickness(rawValue);
                    string newThicknessStr = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", 
                        parsedThickness.Left, parsedThickness.Top, parsedThickness.Right, parsedThickness.Bottom);
                    SetControlProperty(control, basePropName, newThicknessStr);
                }
                else if (component == "top" || component == "right" || component == "bottom" || component == "left")
                {
                    // Longhand margin-top/padding-left/etc.
                    string cleanVal = rawValue.Trim();
                    if (cleanVal.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanVal = cleanVal.Substring(0, cleanVal.Length - 2).Trim();
                    }
                    double valDouble = double.TryParse(cleanVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0.0;

                    double left = currentThickness.Left;
                    double top = currentThickness.Top;
                    double right = currentThickness.Right;
                    double bottom = currentThickness.Bottom;

                    switch (component)
                    {
                        case "top": top = valDouble; break;
                        case "right": right = valDouble; break;
                        case "bottom": bottom = valDouble; break;
                        case "left": left = valDouble; break;
                    }

                    string newThicknessStr = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", left, top, right, bottom);
                    SetControlProperty(control, basePropName, newThicknessStr);
                }
            }
            else
            {
                // Map standard CSS properties to Avalonia properties
                string propName = lowerName switch
                {
                    "width" => "Width",
                    "height" => "Height",
                    "opacity" => "Opacity",
                    "background" => "Background",
                    "background-color" => "Background",
                    "font-size" => "FontSize",
                    "font-family" => "FontFamily",
                    _ => rawName
                };

                SetControlProperty(control, propName, normalizedValue);
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic reflection setting standard CLR properties of controls")]
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

        if (control is Animatable animatable && animatable.Transitions != null)
        {
            var propNames = new System.Collections.Generic.List<string>();
            var durStrings = new System.Collections.Generic.List<string>();

            foreach (var t in animatable.Transitions)
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

    public static object? GetControlProperty(Control control, string name)
    {
        var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(control)
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (avProperty != null)
        {
            return control.GetValue(avProperty);
        }
        return null;
    }

    private static JsonArray GetMatchedRules(Visual visual)
    {
        var matchedCSSRules = new JsonArray();
        if (visual is Control control)
        {
            var stylesToProcess = new List<Avalonia.Styling.Style>();

            // 1. Application-wide styles
            if (Avalonia.Application.Current?.Styles != null)
            {
                foreach (var style in Avalonia.Application.Current.Styles)
                {
                    CollectStyles(style, stylesToProcess);
                }
            }

            // 2. Logical parents (furthest to nearest)
            var parents = new List<StyledElement>();
            var parent = control.Parent;
            while (parent != null)
            {
                parents.Add(parent);
                parent = parent.Parent;
            }
            parents.Reverse();

            foreach (var p in parents)
            {
                if (p.Styles != null)
                {
                    foreach (var style in p.Styles)
                    {
                        CollectStyles(style, stylesToProcess);
                    }
                }
            }

            // 3. Selected control styles
            if (control.Styles != null)
            {
                foreach (var style in control.Styles)
                {
                    CollectStyles(style, stylesToProcess);
                }
            }

            // Now match and extract
            foreach (var style in stylesToProcess)
            {
                var selectorText = style.Selector?.ToString() ?? "";
                if (SelectorMatchesControl(control, selectorText))
                {
                    var cssProperties = new JsonArray();
                    if (style.Setters != null)
                    {
                        foreach (var setter in style.Setters)
                        {
                            if (setter is Setter concreteSetter && concreteSetter.Property != null)
                            {
                                string propName = ToCssPropertyName(concreteSetter.Property.Name);
                                string propVal = concreteSetter.Value?.ToString() ?? "";
                                
                                cssProperties.Add(new JsonObject
                                {
                                    ["name"] = propName,
                                    ["value"] = propVal,
                                    ["important"] = false,
                                    ["implicit"] = false,
                                    ["text"] = $"{propName}: {propVal};",
                                    ["parsedOk"] = true,
                                    ["disabled"] = false
                                });
                            }
                        }
                    }

                    matchedCSSRules.Add(new JsonObject
                    {
                        ["origin"] = "regular",
                        ["selectorList"] = new JsonObject
                        {
                            ["selectors"] = new JsonArray { new JsonObject { ["text"] = selectorText } },
                            ["text"] = selectorText
                        },
                        ["style"] = new JsonObject
                        {
                            ["cssProperties"] = cssProperties,
                            ["shorthandEntries"] = new JsonArray(),
                            ["cssText"] = string.Join(" ", cssProperties.Cast<JsonObject>().Select(p => p["text"]?.GetValue<string>() ?? ""))
                        }
                    });
                }
            }
        }

        return matchedCSSRules;
    }

    private static void CollectStyles(IStyle style, List<Avalonia.Styling.Style> result)
    {
        if (style == null) return;

        if (style is Avalonia.Styling.Style concreteStyle)
        {
            result.Add(concreteStyle);
        }
        else if (style is System.Collections.IEnumerable enumerable)
        {
            foreach (var child in enumerable)
            {
                if (child is IStyle childStyle)
                {
                    CollectStyles(childStyle, result);
                }
            }
        }
    }

    private static bool SelectorMatchesControl(Control control, string selectorText)
    {
        if (string.IsNullOrWhiteSpace(selectorText))
            return false;

        // Split by comma first, since comma separates multiple independent selectors (e.g. "Button.primary, TextBlock.header")
        var independentSelectors = selectorText.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var sel in independentSelectors)
        {
            if (SelectorSegmentMatches(control, sel.Trim()))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SelectorSegmentMatches(Control control, string selector)
    {
        if (string.IsNullOrEmpty(selector)) return false;

        // Split by space/child combinator to find the last segment (the target control)
        var parts = selector.Split(new[] { ' ', '>', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var target = parts[^1].Trim();
        if (string.IsNullOrEmpty(target) || target == "*") return true;

        // Parse target segment components: type name, classes, name, pseudo-classes
        string typeName = "";
        var classes = new List<string>();
        string? nameId = null;

        int i = 0;
        // Parse type name (until first special char: '.', ':', '#', '[')
        while (i < target.Length && target[i] != '.' && target[i] != ':' && target[i] != '#' && target[i] != '[')
        {
            i++;
        }
        if (i > 0)
        {
            typeName = target.Substring(0, i);
        }

        // Parse the rest of the components
        while (i < target.Length)
        {
            char marker = target[i];
            i++;
            int start = i;
            while (i < target.Length && target[i] != '.' && target[i] != ':' && target[i] != '#' && target[i] != '[')
            {
                i++;
            }
            string token = target.Substring(start, i - start);
            if (string.IsNullOrEmpty(token)) continue;

            if (marker == '.')
            {
                classes.Add(token);
            }
            else if (marker == '#')
            {
                nameId = token;
            }
        }

        // Validate Type Name
        if (!string.IsNullOrEmpty(typeName) && typeName != "*")
        {
            var typeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentType = control.GetType();
            while (currentType != null && currentType != typeof(object))
            {
                typeNames.Add(currentType.Name);
                currentType = currentType.BaseType;
            }
            if (!typeNames.Contains(typeName))
            {
                return false;
            }
        }

        // Validate Classes
        foreach (var cls in classes)
        {
            bool hasClass = false;
            foreach (var c in control.Classes)
            {
                if (string.Equals(c, cls, StringComparison.OrdinalIgnoreCase))
                {
                    hasClass = true;
                    break;
                }
            }
            if (!hasClass) return false;
        }

        // Validate Control Name
        if (nameId != null)
        {
            if (!string.Equals(control.Name, nameId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ToCssPropertyName(string avaloniaPropName)
    {
        if (string.IsNullOrEmpty(avaloniaPropName)) return "";
        
        string lowerName = avaloniaPropName.ToLowerInvariant();
        if (lowerName == "background") return "background";
        if (lowerName == "padding") return "padding";
        if (lowerName == "margin") return "margin";
        if (lowerName == "fontsize") return "font-size";
        if (lowerName == "fontfamily") return "font-family";
        if (lowerName == "fontweight") return "font-weight";
        if (lowerName == "foreground") return "color";
        if (lowerName == "borderthickness") return "border-width";
        if (lowerName == "borderbrush") return "border-color";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < avaloniaPropName.Length; i++)
        {
            char c = avaloniaPropName[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('-');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
