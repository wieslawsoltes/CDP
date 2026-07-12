#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CDP.Editor.Nodes.ViewModels;

namespace CdpInspectorApp.ViewModels;

public enum AssertionOperator
{
    Equals,
    NotEquals,
    Contains,
    GreaterThan,
    LessThan,
    Exists,
    NotExists
}

public class ScratchAssertionNodeData
{
    public string? InputNodeId { get; set; }
    public string InputTitle { get; set; } = "";
    public string Path { get; set; } = "";
    public string ExpectedValue { get; set; } = "";
    public AssertionOperator Operator { get; set; } = AssertionOperator.Equals;

    public ScratchAssertionNodeData Clone()
    {
        return new ScratchAssertionNodeData
        {
            InputNodeId = this.InputNodeId,
            InputTitle = this.InputTitle,
            Path = this.Path,
            ExpectedValue = this.ExpectedValue,
            Operator = this.Operator
        };
    }
}

public class ScratchAssertionNodeViewModel : ScratchNodeViewModelBase
{
    private string? _inputNodeId;
    private ScratchNodeViewModelBase? _inputNode;
    private string _inputTitle = "Input";
    private string _path = "";
    private string _expectedValue = "";
    private AssertionOperator _operator = AssertionOperator.Equals;
    private string _outputJson = "{}";

    private bool _passed;
    private string _message = "";
    private string _actualValue = "";

    public string? InputNodeId
    {
        get => _inputNodeId;
        set => RaiseAndSetIfChanged(ref _inputNodeId, value);
    }

    public ScratchNodeViewModelBase? InputNode
    {
        get => _inputNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _inputNode, value))
            {
                if (_inputNodeId != value?.Id)
                {
                    _inputNodeId = value?.Id;
                    OnPropertyChanged(nameof(InputNodeId));
                }
                EvaluateAssertion();
            }
        }
    }

    public string InputTitle
    {
        get => _inputTitle;
        set => RaiseAndSetIfChanged(ref _inputTitle, value);
    }

    public string Path
    {
        get => _path;
        set
        {
            if (RaiseAndSetIfChanged(ref _path, value))
            {
                EvaluateAssertion();
            }
        }
    }

    public string ExpectedValue
    {
        get => _expectedValue;
        set
        {
            if (RaiseAndSetIfChanged(ref _expectedValue, value))
            {
                EvaluateAssertion();
            }
        }
    }

    public AssertionOperator Operator
    {
        get => _operator;
        set
        {
            if (RaiseAndSetIfChanged(ref _operator, value))
            {
                EvaluateAssertion();
            }
        }
    }

    public override string OutputJson => _outputJson;

    public bool Passed
    {
        get => _passed;
        private set => RaiseAndSetIfChanged(ref _passed, value);
    }

    public string Message
    {
        get => _message;
        private set => RaiseAndSetIfChanged(ref _message, value);
    }
    public string ActualValue
    {
        get => _actualValue;
        private set => RaiseAndSetIfChanged(ref _actualValue, value);
    }

    public string PassedText => Passed ? "PASSED" : "FAILED";
    public Avalonia.Media.IBrush PassedBrush => Passed ? Avalonia.Media.Brush.Parse("#81c995") : Avalonia.Media.Brush.Parse("#f28b82");
    public Avalonia.Media.IBrush PassedTextBrush => Passed ? Avalonia.Media.Brush.Parse("#81c995") : Avalonia.Media.Brush.Parse("#f28b82");
    public IEnumerable<AssertionOperator> AllOperators => Enum.GetValues<AssertionOperator>();

    public ScratchAssertionNodeViewModel()
    {
        TitleBackground = Avalonia.Media.Brush.Parse("#b06000");
        BorderBrush = Avalonia.Media.Brush.Parse("#e08000");

        AddInputPin("data", "Data");
        AddOutputPin("status", "Status");

        EvaluateAssertion();
    }

    public void UpdateAssertion(Func<string, ScratchNodeViewModelBase?> getNodeById, IEnumerable<CDP.Editor.Nodes.ViewModels.ConnectionViewModel> connections)
    {
        var incoming = connections
            .Where(c => c.ToNode == this && c.FromNode is ScratchNodeViewModelBase)
            .ToList();

        string? resolvedInputId = InputNodeId;

        // Pin-based lookup
        var dataConn = connections.FirstOrDefault(c => c.ToPin?.Owner == this && c.ToPin.Id == "data");

        if (dataConn?.FromNode is ScratchNodeViewModelBase dataBase)
        {
            resolvedInputId = dataBase.Id;
        }
        else if (string.IsNullOrEmpty(resolvedInputId) && incoming.Count > 0)
        {
            resolvedInputId = incoming[0].FromNode?.Id;
        }

        var inputNode = !string.IsNullOrEmpty(resolvedInputId) ? getNodeById(resolvedInputId) : null;

        bool inputChanged = _inputNode != inputNode;

        if (inputChanged)
        {
            _inputNode = inputNode;
            _inputNodeId = inputNode?.Id;
            OnPropertyChanged(nameof(InputNode));
            OnPropertyChanged(nameof(InputNodeId));
        }

        InputTitle = inputNode != null ? $"{inputNode.Name}" : "Input (No Connection)";
        
        EvaluateAssertion();
    }

    public static JsonNode? ResolvePath(JsonNode? root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path)) return null;

        var current = root;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (current == null) return null;

            var name = part.Trim();
            var bracketIndex = name.IndexOf('[');
            
            if (bracketIndex >= 0)
            {
                var propertyName = name.Substring(0, bracketIndex).Trim();
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (current is JsonObject obj && obj.TryGetPropertyValue(propertyName, out var val))
                    {
                        current = val;
                    }
                    else
                    {
                        return null;
                    }
                }

                var tempPart = name.Substring(bracketIndex);
                while (tempPart.StartsWith('[') && current != null)
                {
                    var closeIndex = tempPart.IndexOf(']');
                    if (closeIndex < 0) return null;

                    var indexContent = tempPart.Substring(1, closeIndex - 1).Trim();
                    
                    if (int.TryParse(indexContent, out int arrayIndex))
                    {
                        if (current is JsonArray arr && arrayIndex >= 0 && arrayIndex < arr.Count)
                        {
                            current = arr[arrayIndex];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        var key = indexContent.Trim('"', '\'');
                        if (current is JsonObject obj && obj.TryGetPropertyValue(key, out var val))
                        {
                            current = val;
                        }
                        else
                        {
                            return null;
                        }
                    }

                    tempPart = tempPart.Substring(closeIndex + 1).Trim();
                }
            }
            else
            {
                if (current is JsonObject obj && obj.TryGetPropertyValue(name, out var val))
                {
                    current = val;
                }
                else
                {
                    return null;
                }
            }
        }

        return current;
    }

    private void EvaluateAssertion()
    {
        if (InputNode == null)
        {
            UpdateOutput(false, "No input connected.");
            return;
        }

        JsonNode? rootNode = InputNode.OutputJsonNode;
        if (rootNode == null)
        {
            var jsonText = InputNode.OutputJson;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                UpdateOutput(false, "Input JSON is empty.");
                return;
            }

            try
            {
                rootNode = JsonNode.Parse(jsonText);
            }
            catch (Exception ex)
            {
                UpdateOutput(false, $"Failed to parse input JSON: {ex.Message}");
                return;
            }
        }

        var resolvedNode = ResolvePath(rootNode, Path);

        if (Operator == AssertionOperator.Exists)
        {
            if (resolvedNode != null)
            {
                UpdateOutput(true, $"Path '{Path}' exists.", resolvedNode);
            }
            else
            {
                UpdateOutput(false, $"Path '{Path}' does not exist.", null);
            }
            return;
        }

        if (Operator == AssertionOperator.NotExists)
        {
            if (resolvedNode == null)
            {
                UpdateOutput(true, $"Path '{Path}' does not exist as expected.", null);
            }
            else
            {
                UpdateOutput(false, $"Path '{Path}' exists but was expected not to.", resolvedNode);
            }
            return;
        }

        if (resolvedNode == null)
        {
            UpdateOutput(false, $"Path '{Path}' resolved to null or was not found.", null);
            return;
        }

        string actualString = resolvedNode is JsonValue val ? val.ToString() : resolvedNode.ToJsonString();
        bool passed = false;
        string message = "";

        switch (Operator)
        {
            case AssertionOperator.Equals:
                passed = string.Equals(actualString, ExpectedValue ?? "", StringComparison.Ordinal);
                message = passed ? $"Value equals expected: '{ExpectedValue}'" : $"Value '{actualString}' does not equal expected: '{ExpectedValue}'";
                break;

            case AssertionOperator.NotEquals:
                passed = !string.Equals(actualString, ExpectedValue ?? "", StringComparison.Ordinal);
                message = passed ? $"Value '{actualString}' does not equal expected '{ExpectedValue}' as expected" : $"Value equals expected: '{ExpectedValue}'";
                break;

            case AssertionOperator.Contains:
                passed = actualString.Contains(ExpectedValue ?? "", StringComparison.Ordinal);
                message = passed ? $"Value '{actualString}' contains expected: '{ExpectedValue}'" : $"Value '{actualString}' does not contain expected: '{ExpectedValue}'";
                break;

            case AssertionOperator.GreaterThan:
                if (double.TryParse(actualString, out double actualDouble) && double.TryParse(ExpectedValue ?? "", out double expectedDouble))
                {
                    passed = actualDouble > expectedDouble;
                    message = passed ? $"{actualDouble} > {expectedDouble}" : $"{actualDouble} is not > {expectedDouble}";
                }
                else
                {
                    passed = false;
                    message = $"Could not parse values for numeric comparison (actual: '{actualString}', expected: '{ExpectedValue}')";
                }
                break;

            case AssertionOperator.LessThan:
                if (double.TryParse(actualString, out double actualDoubleL) && double.TryParse(ExpectedValue ?? "", out double expectedDoubleL))
                {
                    passed = actualDoubleL < expectedDoubleL;
                    message = passed ? $"{actualDoubleL} < {expectedDoubleL}" : $"{actualDoubleL} is not < {expectedDoubleL}";
                }
                else
                {
                    passed = false;
                    message = $"Could not parse values for numeric comparison (actual: '{actualString}', expected: '{ExpectedValue}')";
                }
                break;
        }

        UpdateOutput(passed, message, resolvedNode);
    }

    private void UpdateOutput(bool passed, string message, JsonNode? resolvedValue = null)
    {
        Passed = passed;
        Message = message;
        ActualValue = resolvedValue is JsonValue val ? val.ToString() : (resolvedValue?.ToJsonString() ?? "null");

        var obj = new JsonObject
        {
            ["passed"] = passed,
            ["message"] = message,
            ["path"] = Path,
            ["operator"] = Operator.ToString(),
            ["expected"] = ExpectedValue,
            ["actual"] = resolvedValue?.DeepClone()
        };

        _outputJson = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        OnPropertyChanged(nameof(OutputJson));
        OnPropertyChanged(nameof(PassedBrush));
        OnPropertyChanged(nameof(PassedTextBrush));
        OnPropertyChanged(nameof(PassedText));
    }
}
