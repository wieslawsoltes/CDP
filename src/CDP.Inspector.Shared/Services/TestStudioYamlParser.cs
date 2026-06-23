using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CdpInspectorApp.Models;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace CdpInspectorApp.Services;

public static class TestStudioYamlParser
{
    private class ForceDoubleQuoteEmitter : ChainedEventEmitter
    {
        private static readonly HashSet<string> UnquotedStrings = new(
            FlowCommandCatalog.Commands.Select(c => c.Name)
                .Concat(FlowCommandCatalog.SelectorKeys)
                .Concat(new[]
                {
                    "selector", "element", "cropOn", "from", "text", "value", "direction", "amount",
                    "maxScrolls", "timeout", "speed", "visibilityPercentage", "centerElement",
                    "appId", "description", "env", "tags", "start", "end", "latitude", "longitude",
                    "accuracy", "orientation", "key", "file", "label", "commands", "while",
                    "visible", "notVisible", "optional", "permissions", "all", "path", "query",
                    "outputVariable", "thresholdPercentage", "repeat", "delay", "retryTapIfNoChange",
                    "waitToSettleTimeoutMs", "targetSelector", "offsetX", "offsetY",
                    "targetOffsetX", "targetOffsetY", "dragAndDrop"
                }),
            StringComparer.OrdinalIgnoreCase);

        public ForceDoubleQuoteEmitter(IEventEmitter nextEmitter) : base(nextEmitter) { }

        public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Value is string strVal)
            {
                if (!UnquotedStrings.Contains(strVal))
                {
                    eventInfo.Style = ScalarStyle.DoubleQuoted;
                }
            }
            base.Emit(eventInfo, emitter);
        }
    }

    public static List<TestStudioStepModel> Parse(string yaml, out string appId, out string description)
    {
        appId = "";
        description = "";
        var steps = new List<TestStudioStepModel>();

        if (string.IsNullOrEmpty(yaml))
        {
            return steps;
        }

        var yamlStream = new YamlStream();
        using (var reader = new StringReader(yaml))
        {
            yamlStream.Load(reader);
        }

        foreach (var doc in yamlStream.Documents)
        {
            if (doc.RootNode is YamlMappingNode mappingNode)
            {
                foreach (var entry in mappingNode.Children)
                {
                    if (entry.Key is YamlScalarNode scalarKey)
                    {
                        var key = scalarKey.Value;
                        if (key == "appId" && entry.Value is YamlScalarNode scalarAppId)
                        {
                            appId = scalarAppId.Value ?? "";
                        }
                        else if (key == "description" && entry.Value is YamlScalarNode scalarDesc)
                        {
                            description = scalarDesc.Value ?? "";
                        }
                    }
                }
            }
            else if (doc.RootNode is YamlSequenceNode sequenceNode)
            {
                foreach (var node in sequenceNode.Children)
                {
                    var step = ParseStepNode(node);
                    if (step != null)
                    {
                        steps.Add(step);
                    }
                }
            }
        }

        return steps;
    }

    private static TestStudioStepModel? ParseStepNode(YamlNode node)
    {
        TestStudioStepModel? model = null;
        if (node is YamlScalarNode scalarNode)
        {
            var action = scalarNode.Value ?? "";
            model = BuildStepModel(action, "", null);
        }
        else if (node is YamlMappingNode mappingNode)
        {
            string? action = null;
            string inlineValue = "";
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            ObservableCollection<TestStudioStepModel>? nestedSteps = null;

            foreach (var entry in mappingNode.Children)
            {
                if (entry.Key is YamlScalarNode scalarKey && scalarKey.Value != null && IsKnownAction(scalarKey.Value))
                {
                    action = scalarKey.Value;
                    if (entry.Value is YamlScalarNode scalarVal)
                    {
                        inlineValue = scalarVal.Value ?? "";
                    }
                    else if (entry.Value is YamlMappingNode nestedMapping)
                    {
                        parameters = ReadMapping(nestedMapping);
                        CopyScalarParameters(parameters, dict);
                        nestedSteps = ReadNestedCommands(nestedMapping);
                        ReadWhileCondition(nestedMapping, dict);
                    }
                    else if (entry.Value is YamlSequenceNode sequenceValue)
                    {
                        parameters["items"] = ReadSequence(sequenceValue);
                    }
                    break;
                }
            }

            if (action == null && mappingNode.Children.Count > 0)
            {
                var firstEntry = mappingNode.Children[0];
                if (firstEntry.Key is YamlScalarNode scalarKey && scalarKey.Value != null)
                {
                    action = scalarKey.Value;
                    if (firstEntry.Value is YamlScalarNode scalarVal)
                    {
                        inlineValue = scalarVal.Value ?? "";
                    }
                    else if (firstEntry.Value is YamlMappingNode nestedMapping)
                    {
                        parameters = ReadMapping(nestedMapping);
                        CopyScalarParameters(parameters, dict);
                        nestedSteps = ReadNestedCommands(nestedMapping);
                        ReadWhileCondition(nestedMapping, dict);
                    }
                    else if (firstEntry.Value is YamlSequenceNode sequenceValue)
                    {
                        parameters["items"] = ReadSequence(sequenceValue);
                    }
                }
            }

            if (action != null)
            {
                model = BuildStepModel(action, inlineValue, dict, nestedSteps, parameters);
            }
        }

        if (model != null)
        {
            model.StartLine = (int)node.Start.Line;
            model.EndLine = (int)node.End.Line;
        }
        return model;
    }

    private static Dictionary<string, object?> ReadMapping(YamlMappingNode mappingNode)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in mappingNode.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode || string.IsNullOrEmpty(keyNode.Value))
            {
                continue;
            }

            result[keyNode.Value] = ReadYamlValue(entry.Value);
        }

        return result;
    }

    private static List<object?> ReadSequence(YamlSequenceNode sequenceNode)
    {
        var result = new List<object?>();
        foreach (var child in sequenceNode.Children)
        {
            result.Add(ReadYamlValue(child));
        }

        return result;
    }

    private static object? ReadYamlValue(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => scalar.Value ?? "",
            YamlMappingNode mapping => ReadMapping(mapping),
            YamlSequenceNode sequence => ReadSequence(sequence),
            _ => null
        };
    }

    private static void CopyScalarParameters(Dictionary<string, object?> parameters, Dictionary<string, string> dict)
    {
        foreach (var kv in parameters)
        {
            if (kv.Value is string str)
            {
                dict[kv.Key] = str;
            }
            else if (kv.Value is bool b)
            {
                dict[kv.Key] = b.ToString().ToLowerInvariant();
            }
            else if (kv.Value is int or long or double or decimal or float)
            {
                dict[kv.Key] = FlowCommandCatalog.ScalarToString(kv.Value);
            }
        }
    }

    private static ObservableCollection<TestStudioStepModel>? ReadNestedCommands(YamlMappingNode mappingNode)
    {
        foreach (var nestedEntry in mappingNode.Children)
        {
            if (nestedEntry.Key is YamlScalarNode nestedKey &&
                nestedKey.Value == "commands" &&
                nestedEntry.Value is YamlSequenceNode seqNode)
            {
                var nestedSteps = new ObservableCollection<TestStudioStepModel>();
                foreach (var childNode in seqNode.Children)
                {
                    var childStep = ParseStepNode(childNode);
                    if (childStep != null)
                    {
                        nestedSteps.Add(childStep);
                    }
                }

                return nestedSteps;
            }
        }

        return null;
    }

    private static void ReadWhileCondition(YamlMappingNode mappingNode, Dictionary<string, string> dict)
    {
        foreach (var nestedEntry in mappingNode.Children)
        {
            if (nestedEntry.Key is not YamlScalarNode nestedKey ||
                nestedKey.Value != "while" ||
                nestedEntry.Value is not YamlMappingNode whileMapping)
            {
                continue;
            }

            foreach (var whileEntry in whileMapping.Children)
            {
                if (whileEntry.Key is YamlScalarNode whileKey && whileEntry.Value is YamlScalarNode whileVal)
                {
                    dict["while_type"] = whileKey.Value ?? "";
                    dict["while_value"] = whileVal.Value ?? "";
                    return;
                }
            }
        }
    }

    private static bool IsKnownAction(string action)
    {
        return FlowCommandCatalog.IsKnownCommand(action) ||
            action.Equals("dragAndDrop", StringComparison.OrdinalIgnoreCase);
    }

    private static TestStudioStepModel BuildStepModel(string action, string inlineValue, Dictionary<string, string>? dict = null, ObservableCollection<TestStudioStepModel>? nestedSteps = null, Dictionary<string, object?>? parameters = null)
    {
        dict ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        parameters ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var model = new TestStudioStepModel { Action = FlowCommandCatalog.CanonicalizeAction(action), Parameters = parameters };
        var selectorDisplay = FlowCommandCatalog.BuildSelectorDisplay(parameters);
        var valueDisplay = FlowCommandCatalog.BuildValueDisplay(parameters);

        switch (action.ToLowerInvariant())
        {
            case "launchapp":
                model.Action = "launchApp";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "tapon":
            case "doubletapon":
            case "longpresson":
                model.Action = action.Equals("doubleTapOn", StringComparison.OrdinalIgnoreCase) ? "doubleTapOn" :
                               action.Equals("longPressOn", StringComparison.OrdinalIgnoreCase) ? "longPressOn" : "tapOn";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Selector = inlineValue;
                    model.Value = "";
                }
                else if (!string.IsNullOrEmpty(selectorDisplay))
                {
                    model.Selector = selectorDisplay;
                    model.Value = valueDisplay;
                }
                else if (dict.TryGetValue("selector", out var sel))
                {
                    model.Selector = sel;
                    model.Value = "";
                }
                else if (dict.TryGetValue("x", out var x) && dict.TryGetValue("y", out var y))
                {
                    model.Selector = "";
                    model.Value = $"{x}, {y}";
                }
                else
                {
                    model.Selector = "";
                    model.Value = "";
                }
                break;

            case "inputtext":
                model.Action = "inputText";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Selector = "";
                    model.Value = inlineValue;
                }
                else
                {
                    model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                    model.Value = dict.GetValueOrDefault("text", dict.GetValueOrDefault("value", valueDisplay));
                }
                break;

            case "cleartext":
                model.Action = "clearText";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Selector = inlineValue;
                }
                else
                {
                    model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                }
                model.Value = "";
                break;

            case "pastetext":
                model.Action = "pasteText";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "erasetext":
                model.Action = "eraseText";
                model.Selector = "";
                model.Value = !string.IsNullOrEmpty(inlineValue) ? inlineValue : dict.GetValueOrDefault("value", dict.GetValueOrDefault("amount", "1"));
                break;

            case "draganddrop":
                model.Action = "dragAndDrop";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Selector = inlineValue;
                    model.Value = "";
                }
                else
                {
                    model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                    var parts = new List<string>();
                    if (dict.TryGetValue("targetSelector", out var targetSelector)) parts.Add($"targetSelector: {targetSelector}");
                    if (dict.TryGetValue("offsetX", out var ox)) parts.Add($"offsetX: {ox}");
                    if (dict.TryGetValue("offsetY", out var oy)) parts.Add($"offsetY: {oy}");
                    if (dict.TryGetValue("targetOffsetX", out var tox)) parts.Add($"targetOffsetX: {tox}");
                    if (dict.TryGetValue("targetOffsetY", out var toy)) parts.Add($"targetOffsetY: {toy}");
                    model.Value = string.Join(", ", parts);
                }
                break;

            case "swipe":
                model.Action = "swipe";
                model.Selector = "";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Value = inlineValue;
                }
                else
                {
                    var parts = new List<string>();
                    if (dict.TryGetValue("start", out var start)) parts.Add($"start: {start}");
                    if (dict.TryGetValue("end", out var end)) parts.Add($"end: {end}");
                    if (dict.TryGetValue("direction", out var sDir)) parts.Add($"direction: {sDir}");
                    model.Value = string.Join(", ", parts);
                }
                break;

            case "stopapp":
            case "killapp":
                model.Action = action.Equals("stopApp", StringComparison.OrdinalIgnoreCase) ? "stopApp" : "killApp";
                model.Selector = "";
                model.Value = !string.IsNullOrEmpty(inlineValue) ? inlineValue : dict.GetValueOrDefault("appId", "");
                break;

            case "clearstate":
                model.Action = "clearState";
                model.Selector = "";
                model.Value = !string.IsNullOrEmpty(inlineValue) ? inlineValue : dict.GetValueOrDefault("appId", "");
                break;

            case "setorientation":
                model.Action = "setOrientation";
                model.Selector = "";
                model.Value = !string.IsNullOrEmpty(inlineValue) ? inlineValue : dict.GetValueOrDefault("orientation", "");
                break;

            case "setlocation":
                model.Action = "setLocation";
                model.Selector = "";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Value = inlineValue;
                }
                else
                {
                    var parts = new List<string>();
                    if (dict.TryGetValue("latitude", out var lat)) parts.Add($"latitude: {lat}");
                    if (dict.TryGetValue("longitude", out var lon)) parts.Add($"longitude: {lon}");
                    model.Value = string.Join(", ", parts);
                }
                break;

            case "takescreenshot":
                model.Action = "takeScreenshot";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "openlink":
                model.Action = "openLink";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "copytextfrom":
                model.Action = "copyTextFrom";
                model.Selector = !string.IsNullOrEmpty(inlineValue) ? inlineValue : (!string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", ""));
                model.Value = "";
                break;

            case "assertvisible":
                model.Action = "assertVisible";
                model.Selector = !string.IsNullOrEmpty(inlineValue) ? inlineValue : (!string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", ""));
                model.Value = valueDisplay;
                break;

            case "assertnotvisible":
                model.Action = "assertNotVisible";
                model.Selector = !string.IsNullOrEmpty(inlineValue) ? inlineValue : (!string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", ""));
                model.Value = valueDisplay;
                break;

            case "asserttrue":
                model.Action = "assertTrue";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "assertfalse":
                model.Action = "assertFalse";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "setairplanemode":
                model.Action = "setAirplaneMode";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "delay":
                model.Action = "delay";
                model.Selector = "";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Value = inlineValue;
                }
                else
                {
                    model.Value = dict.GetValueOrDefault("value", dict.GetValueOrDefault("amount", ""));
                }
                break;

            case "back":
                model.Action = "back";
                model.Selector = "";
                model.Value = "";
                break;

            case "scroll":
                model.Action = "scroll";
                model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Value = inlineValue;
                }
                else
                {
                    var parts = new List<string>();
                    if (dict.TryGetValue("direction", out var dir)) parts.Add($"direction: {dir}");
                    if (dict.TryGetValue("amount", out var amt)) parts.Add($"amount: {amt}");
                    model.Value = string.Join(", ", parts);
                }
                break;

            case "scrolluntilvisible":
                model.Action = "scrollUntilVisible";
                model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                var suvParts = new List<string>();
                if (dict.TryGetValue("direction", out var sdir)) suvParts.Add($"direction: {sdir}");
                if (dict.TryGetValue("maxscrolls", out var ms)) suvParts.Add($"maxScrolls: {ms}");
                if (dict.TryGetValue("timeout", out var timeout)) suvParts.Add($"timeout: {timeout}");
                if (dict.TryGetValue("speed", out var speed)) suvParts.Add($"speed: {speed}");
                if (dict.TryGetValue("visibilityPercentage", out var visibilityPercentage)) suvParts.Add($"visibilityPercentage: {visibilityPercentage}");
                if (dict.TryGetValue("centerElement", out var centerElement)) suvParts.Add($"centerElement: {centerElement}");
                model.Value = string.Join(", ", suvParts);
                break;

            case "presskey":
                model.Action = "pressKey";
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Selector = "";
                    model.Value = inlineValue;
                }
                else
                {
                    model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                    model.Value = dict.GetValueOrDefault("value", dict.GetValueOrDefault("key", ""));
                }
                break;

            case "repeat":
            case "retry":
                model.Action = action.Equals("repeat", StringComparison.OrdinalIgnoreCase) ? "repeat" : "retry";
                model.Selector = "";
                model.Value = !string.IsNullOrEmpty(inlineValue) ? inlineValue : dict.GetValueOrDefault("times", dict.GetValueOrDefault("maxRetries", dict.GetValueOrDefault("value", "1")));
                if (dict.TryGetValue("while_type", out var wt) && dict.TryGetValue("while_value", out var wv))
                {
                    model.WhileConditionType = wt;
                    model.WhileConditionValue = wv;
                }
                if (nestedSteps != null)
                {
                    model.NestedSteps = nestedSteps;
                }
                break;

            case "runflow":
                model.Action = "runFlow";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            case "evalscript":
            case "runscript":
                model.Action = action.Equals("evalScript", StringComparison.OrdinalIgnoreCase) ? "evalScript" : "runScript";
                model.Selector = "";
                model.Value = inlineValue;
                break;

            default:
                if (!string.IsNullOrEmpty(inlineValue))
                {
                    model.Selector = "";
                    model.Value = inlineValue;
                }
                else
                {
                    model.Selector = !string.IsNullOrEmpty(selectorDisplay) ? selectorDisplay : dict.GetValueOrDefault("selector", "");
                    var parts = new List<string>();
                    foreach (var kv in dict)
                    {
                        if (kv.Key.Equals("selector", StringComparison.OrdinalIgnoreCase)) continue;
                        parts.Add($"{kv.Key}: {kv.Value}");
                    }
                    model.Value = string.Join(", ", parts);
                }
                break;
        }

        return model;
    }

    public static string Generate(List<TestStudioStepModel> steps, string appId, string description)
    {
        var sb = new StringBuilder();
        var serializer = new SerializerBuilder()
            .WithEventEmitter(nextEmitter => new ForceDoubleQuoteEmitter(nextEmitter))
            .Build();

        bool hasMetadata = !string.IsNullOrEmpty(appId) || !string.IsNullOrEmpty(description);
        if (hasMetadata)
        {
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(appId)) metadata["appId"] = appId;
            if (!string.IsNullOrEmpty(description)) metadata["description"] = description;

            var metadataYaml = serializer.Serialize(metadata);
            sb.Append(metadataYaml);
            sb.AppendLine("---");
        }

        if (steps != null && steps.Count > 0)
        {
            var stepsList = new List<object>();
            foreach (var step in steps)
            {
                var serialized = SerializeStep(step);
                if (serialized != null && !(serialized is string s && string.IsNullOrEmpty(s)))
                {
                    stepsList.Add(serialized);
                }
            }

            var stepsYaml = serializer.Serialize(stepsList);
            sb.Append(stepsYaml);
        }

        return sb.ToString();
    }

    private static object SerializeStep(TestStudioStepModel step)
    {
        var action = step.Action;
        if (string.IsNullOrEmpty(action)) return "";

        if (step.Parameters.Count > 0)
        {
            var parameters = ToSerializableDictionary(step.Parameters);
            if ((action == "repeat" || action == "retry" || action == "runFlow") &&
                step.NestedSteps != null &&
                step.NestedSteps.Count > 0)
            {
                parameters["commands"] = step.NestedSteps.Select(SerializeStep).ToList();
            }

            if (action == "repeat" &&
                !string.IsNullOrEmpty(step.WhileConditionType) &&
                !string.IsNullOrEmpty(step.WhileConditionValue))
            {
                parameters["while"] = new Dictionary<string, object?>
                {
                    { step.WhileConditionType, step.WhileConditionValue }
                };
            }

            if (action == "addMedia" &&
                parameters.Count == 1 &&
                parameters.TryGetValue("items", out var items) &&
                items is List<object?>)
            {
                return new Dictionary<string, object?> { { action, items } };
            }

            return new Dictionary<string, object?> { { action, parameters } };
        }

        if (action == "launchApp" || action == "back")
        {
            if (!string.IsNullOrEmpty(step.Value))
            {
                return new Dictionary<string, string> { { action, step.Value } };
            }
            else
            {
                return action;
            }
        }
        else if (action == "tapOn" || action == "doubleTapOn" || action == "longPressOn")
        {
            if (!string.IsNullOrEmpty(step.Selector))
            {
                return new Dictionary<string, string> { { action, step.Selector } };
            }
            else if (!string.IsNullOrEmpty(step.Value))
            {
                var coords = ParseCoordinates(step.Value);
                if (coords != null)
                {
                    object xVal = int.TryParse(coords.Value.x, out int xInt) ? xInt : (object)coords.Value.x;
                    object yVal = int.TryParse(coords.Value.y, out int yInt) ? yInt : (object)coords.Value.y;
                    return new Dictionary<string, object>
                    {
                        {
                            action, new Dictionary<string, object>
                            {
                                { "x", xVal },
                                { "y", yVal }
                            }
                        }
                    };
                }
                else
                {
                    return new Dictionary<string, string> { { action, step.Value } };
                }
            }
            else
            {
                return action;
            }
        }
        else if (action == "inputText")
        {
            if (!string.IsNullOrEmpty(step.Selector))
            {
                return new Dictionary<string, object>
                {
                    {
                        "inputText", new Dictionary<string, string>
                        {
                            { "selector", step.Selector },
                            { "text", step.Value ?? "" }
                        }
                    }
                };
            }
            else
            {
                return new Dictionary<string, string> { { "inputText", step.Value ?? "" } };
            }
        }
        else if (action == "clearText")
        {
            if (!string.IsNullOrEmpty(step.Selector))
            {
                return new Dictionary<string, string> { { "clearText", step.Selector } };
            }
            else
            {
                return "clearText";
            }
        }
        else if (action == "pasteText")
        {
            if (!string.IsNullOrEmpty(step.Value))
            {
                return new Dictionary<string, string> { { "pasteText", step.Value } };
            }
            else
            {
                return "pasteText";
            }
        }
        else if (action == "eraseText")
        {
            object val = int.TryParse(step.Value, out int count) ? count : (object)(step.Value ?? "1");
            return new Dictionary<string, object> { { "eraseText", val } };
        }
        else if (action == "swipe")
        {
            var props = ParseKeyValuePairs(step.Value);
            if (props.Count > 0)
            {
                var swipeDict = new Dictionary<string, object>();
                foreach (var kv in props)
                {
                    swipeDict[kv.Key] = kv.Value;
                }
                return new Dictionary<string, object> { { "swipe", swipeDict } };
            }
            else
            {
                return new Dictionary<string, string> { { "swipe", step.Value ?? "" } };
            }
        }
        else if (action == "dragAndDrop")
        {
            var props = ParseKeyValuePairs(step.Value);
            if (props.Count > 0 || !string.IsNullOrEmpty(step.Selector))
            {
                var dragDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    dragDict["selector"] = step.Selector;
                }
                foreach (var kv in props)
                {
                    if (kv.Key == "offsetX" || kv.Key == "offsetY" || kv.Key == "targetOffsetX" || kv.Key == "targetOffsetY")
                    {
                        dragDict[kv.Key] = double.TryParse(kv.Value, out double val) ? val : (object)kv.Value;
                    }
                    else
                    {
                        dragDict[kv.Key] = kv.Value;
                    }
                }
                return new Dictionary<string, object> { { "dragAndDrop", dragDict } };
            }
            else if (!string.IsNullOrEmpty(step.Value))
            {
                return new Dictionary<string, string> { { "dragAndDrop", step.Value } };
            }
            else
            {
                return "dragAndDrop";
            }
        }
        else if (action == "stopApp" || action == "killApp" || action == "clearState" || action == "setOrientation" || action == "takeScreenshot" || action == "assertTrue" || action == "assertFalse" || action == "setAirplaneMode" || action == "runFlow" || action == "evalScript" || action == "runScript" || action == "openLink")
        {
            return new Dictionary<string, string> { { action, step.Value ?? "" } };
        }
        else if (action == "copyTextFrom")
        {
            return new Dictionary<string, string> { { "copyTextFrom", step.Selector ?? "" } };
        }
        else if (action == "repeat" || action == "retry")
        {
            var loopDict = new Dictionary<string, object>();
            if (action == "repeat")
            {
                loopDict["times"] = int.TryParse(step.Value, out int count) ? count : (object)(step.Value ?? "1");
                if (!string.IsNullOrEmpty(step.WhileConditionType) && !string.IsNullOrEmpty(step.WhileConditionValue))
                {
                    loopDict["while"] = new Dictionary<string, string>
                    {
                        { step.WhileConditionType, step.WhileConditionValue }
                    };
                }
            }
            else // retry
            {
                loopDict["maxRetries"] = int.TryParse(step.Value, out int count) ? count : (object)(step.Value ?? "1");
            }

            if (step.NestedSteps != null && step.NestedSteps.Count > 0)
            {
                loopDict["commands"] = step.NestedSteps.Select(SerializeStep).ToList();
            }
            return new Dictionary<string, object> { { action, loopDict } };
        }
        else if (action == "setLocation")
        {
            var props = ParseKeyValuePairs(step.Value);
            if (props.Count > 0)
            {
                var locDict = new Dictionary<string, object>();
                foreach (var kv in props)
                {
                    locDict[kv.Key] = double.TryParse(kv.Value, out double val) ? val : (object)kv.Value;
                }
                return new Dictionary<string, object> { { "setLocation", locDict } };
            }
            else
            {
                return new Dictionary<string, string> { { "setLocation", step.Value ?? "" } };
            }
        }
        else if (action == "assertVisible" || action == "assertNotVisible")
        {
            return new Dictionary<string, string> { { action, step.Selector ?? "" } };
        }
        else if (action == "delay")
        {
            object delayVal = int.TryParse(step.Value, out int delayInt) ? delayInt : (object)(step.Value ?? "0");
            return new Dictionary<string, object> { { "delay", delayVal } };
        }
        else if (action == "scroll")
        {
            var props = ParseKeyValuePairs(step.Value);
            if (props.Count > 0 || !string.IsNullOrEmpty(step.Selector))
            {
                var scrollDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    scrollDict["selector"] = step.Selector;
                }
                foreach (var kv in props)
                {
                    scrollDict[kv.Key] = int.TryParse(kv.Value, out int val) ? val : (object)kv.Value;
                }
                return new Dictionary<string, object> { { "scroll", scrollDict } };
            }
            else if (!string.IsNullOrEmpty(step.Value))
            {
                return new Dictionary<string, object> { { "scroll", int.TryParse(step.Value, out int val) ? val : (object)step.Value } };
            }
            else
            {
                return "scroll";
            }
        }
        else if (action == "scrollUntilVisible")
        {
            var suvDict = new Dictionary<string, object>
            {
                { "selector", step.Selector ?? "" }
            };
            var props = ParseKeyValuePairs(step.Value);
            foreach (var kv in props)
            {
                suvDict[kv.Key] = int.TryParse(kv.Value, out int val) ? val : (object)kv.Value;
            }
            return new Dictionary<string, object> { { "scrollUntilVisible", suvDict } };
        }
        else if (action == "pressKey")
        {
            if (!string.IsNullOrEmpty(step.Selector))
            {
                return new Dictionary<string, object>
                {
                    {
                        "pressKey", new Dictionary<string, string>
                        {
                            { "selector", step.Selector },
                            { "value", step.Value ?? "" }
                        }
                    }
                };
            }
            else
            {
                return new Dictionary<string, string> { { "pressKey", step.Value ?? "" } };
            }
        }
        else
        {
            if (string.IsNullOrEmpty(step.Selector) && string.IsNullOrEmpty(step.Value))
            {
                return action;
            }
            else if (!string.IsNullOrEmpty(step.Selector) && string.IsNullOrEmpty(step.Value))
            {
                return new Dictionary<string, string> { { action, step.Selector } };
            }
            else if (string.IsNullOrEmpty(step.Selector) && !string.IsNullOrEmpty(step.Value))
            {
                var props = ParseKeyValuePairs(step.Value);
                if (props.Count > 0)
                {
                    var actionDict = new Dictionary<string, object>();
                    foreach (var kv in props)
                    {
                        actionDict[kv.Key] = int.TryParse(kv.Value, out int val) ? val : (object)kv.Value;
                    }
                    return new Dictionary<string, object> { { action, actionDict } };
                }
                else
                {
                    return new Dictionary<string, string> { { action, step.Value } };
                }
            }
            else
            {
                var actionDict = new Dictionary<string, object>
                {
                    { "selector", step.Selector ?? "" }
                };
                var props = ParseKeyValuePairs(step.Value);
                if (props.Count > 0)
                {
                    foreach (var kv in props)
                    {
                        actionDict[kv.Key] = int.TryParse(kv.Value, out int val) ? val : (object)(kv.Value ?? "");
                    }
                }
                else
                {
                    actionDict["value"] = step.Value ?? "";
                }
                return new Dictionary<string, object> { { action, actionDict } };
            }
        }
    }

    private static Dictionary<string, object?> ToSerializableDictionary(Dictionary<string, object?> source)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source)
        {
            result[kv.Key] = ToSerializableValue(kv.Value);
        }

        return result;
    }

    private static object? ToSerializableValue(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dict => ToSerializableDictionary(dict),
            IReadOnlyDictionary<string, object?> readOnlyDict => readOnlyDict.ToDictionary(kv => kv.Key, kv => ToSerializableValue(kv.Value), StringComparer.OrdinalIgnoreCase),
            List<object?> list => list.Select(ToSerializableValue).ToList(),
            IReadOnlyList<object?> list => list.Select(ToSerializableValue).ToList(),
            _ => value
        };
    }

    private static (string x, string y)? ParseCoordinates(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var xPart = parts[0].Trim();
            var yPart = parts[1].Trim();

            var xVal = ExtractNumber(xPart);
            var yVal = ExtractNumber(yPart);
            if (!string.IsNullOrEmpty(xVal) && !string.IsNullOrEmpty(yVal))
            {
                return (xVal, yVal);
            }
        }
        return null;
    }

    private static string ExtractNumber(string input)
    {
        int idx = input.IndexOfAny(new[] { ':', '=' });
        if (idx >= 0)
        {
            input = input.Substring(idx + 1).Trim();
        }
        return input.Trim(' ', '"', '\'');
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string? value)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(value)) return dict;

        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOfAny(new[] { ':', '=' });
            if (idx > 0)
            {
                var key = part.Substring(0, idx).Trim();
                var val = part.Substring(idx + 1).Trim();
                dict[key] = CleanValue(val);
            }
        }
        return dict;
    }

    private static string CleanValue(string val)
    {
        if (val == null) return "";
        val = val.Trim();
        if (val.StartsWith("\"") && val.EndsWith("\""))
        {
            val = val.Substring(1, val.Length - 2);
        }
        else if (val.StartsWith("'") && val.EndsWith("'"))
        {
            val = val.Substring(1, val.Length - 2);
        }
        return val;
    }
}
