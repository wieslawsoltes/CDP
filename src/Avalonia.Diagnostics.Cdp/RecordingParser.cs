using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Avalonia.Diagnostics.Cdp;

public class ParsedStep
{
    public string Type { get; set; } = "";
    public string Selector { get; set; } = "";
    public string Value { get; set; } = "";
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Url { get; set; } = "";
    public string Key { get; set; } = "";
}

public static class RecordingParser
{
    public static List<ParsedStep> Parse(string content)
    {
        var steps = new List<ParsedStep>();
        if (string.IsNullOrWhiteSpace(content)) return steps;

        // Try to parse as JSON first
        bool isJson = false;
        try
        {
            var root = JsonNode.Parse(content) as JsonObject;
            var stepsNode = root?["steps"] as JsonArray;
            if (stepsNode != null)
            {
                isJson = true;
                foreach (var stepNode in stepsNode)
                {
                    var stepObj = stepNode as JsonObject;
                    if (stepObj == null) continue;

                    string type = stepObj["type"]?.GetValue<string>() ?? "";
                    string value = stepObj["value"]?.GetValue<string>() ?? "";
                    double offsetX = stepObj["offsetX"]?.GetValue<double>() ?? 0;
                    double offsetY = stepObj["offsetY"]?.GetValue<double>() ?? 0;
                    double width = stepObj["width"]?.GetValue<double>() ?? 0;
                    double height = stepObj["height"]?.GetValue<double>() ?? 0;
                    string url = stepObj["url"]?.GetValue<string>() ?? "";
                    string keyVal = stepObj["key"]?.GetValue<string>() ?? "";

                    string selector = "";
                    var selectorsArr = stepObj["selectors"] as JsonArray;
                    if (selectorsArr != null && selectorsArr.Count > 0)
                    {
                        var firstGroup = selectorsArr[0] as JsonArray;
                        if (firstGroup != null && firstGroup.Count > 0)
                        {
                            selector = firstGroup[0]?.GetValue<string>() ?? "";
                        }
                    }

                    steps.Add(new ParsedStep
                    {
                        Type = type,
                        Selector = selector,
                        Value = value,
                        OffsetX = offsetX,
                        OffsetY = offsetY,
                        Width = width,
                        Height = height,
                        Url = url,
                        Key = keyVal
                    });
                }
            }
        }
        catch
        {
            // Fall through to JS parser
        }

        if (!isJson)
        {
            // Parse as Puppeteer JS script
            var varToSelector = new Dictionary<string, string>();
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            var selectorRegex = new Regex(@"const\s+(\w+)\s*=\s*await\s+page\.waitForSelector\('([^']+)'\);", RegexOptions.Compiled);
            var clickRegex = new Regex(@"await\s+(\w+)\.click\(\);", RegexOptions.Compiled);
            var typeRegex = new Regex(@"await\s+(\w+)\.type\('([^']*)'\);", RegexOptions.Compiled);
            var viewportRegex = new Regex(@"await\s+page\.setViewport\(\{\s*width:\s*(\d+),\s*height:\s*(\d+)\s*\}\);", RegexOptions.Compiled);
            var gotoRegex = new Regex(@"await\s+page\.goto\('([^']+)'\);", RegexOptions.Compiled);
            var keypressRegex = new Regex(@"await\s+page\.keyboard\.press\('([^']+)'\);", RegexOptions.Compiled);

            foreach (var line in lines)
            {
                var vpMatch = viewportRegex.Match(line);
                if (vpMatch.Success)
                {
                    steps.Add(new ParsedStep
                    {
                        Type = "setViewport",
                        Width = double.Parse(vpMatch.Groups[1].Value),
                        Height = double.Parse(vpMatch.Groups[2].Value)
                    });
                    continue;
                }

                var gotoMatch = gotoRegex.Match(line);
                if (gotoMatch.Success)
                {
                    steps.Add(new ParsedStep
                    {
                        Type = "navigate",
                        Url = gotoMatch.Groups[1].Value
                    });
                    continue;
                }

                var kpMatch = keypressRegex.Match(line);
                if (kpMatch.Success)
                {
                    steps.Add(new ParsedStep
                    {
                        Type = "keydown",
                        Key = kpMatch.Groups[1].Value
                    });
                    continue;
                }

                var selMatch = selectorRegex.Match(line);
                if (selMatch.Success)
                {
                    string varName = selMatch.Groups[1].Value;
                    string selector = selMatch.Groups[2].Value;
                    varToSelector[varName] = selector;
                    continue;
                }

                var clickMatch = clickRegex.Match(line);
                if (clickMatch.Success)
                {
                    string varName = clickMatch.Groups[1].Value;
                    if (varToSelector.TryGetValue(varName, out string? selector))
                    {
                        steps.Add(new ParsedStep
                        {
                            Type = "click",
                            Selector = selector,
                            OffsetX = 0,
                            OffsetY = 0
                        });
                    }
                    continue;
                }

                var typeMatch = typeRegex.Match(line);
                if (typeMatch.Success)
                {
                    string varName = typeMatch.Groups[1].Value;
                    string textVal = typeMatch.Groups[2].Value;
                    if (varToSelector.TryGetValue(varName, out string? selector))
                    {
                        steps.Add(new ParsedStep
                        {
                            Type = "change",
                            Selector = selector,
                            Value = textVal
                        });
                    }
                    continue;
                }
            }
        }

        return steps;
    }
}
