using System.Collections.ObjectModel;
using CdpInspectorApp.ViewModels;
using Chrome.DevTools.Protocol.Inspector;

namespace CdpInspectorApp.Models;

public sealed class V8ScriptModel
{
    public string ScriptId { get; init; } = "";
    public string Url { get; init; } = "";
    public string Hash { get; init; } = "";
    public string SourceMapUrl { get; init; } = "";
    public int ExecutionContextId { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
    public int Length { get; init; }
    public bool IsModule { get; init; }
    public bool IsOriginalSource { get; init; }
    public string GeneratedScriptId { get; init; } = "";
    public string GeneratedUrl { get; init; } = "";
    public int SourceIndex { get; init; } = -1;
    public string? SourceContent { get; init; }
    public V8SourceMap? SourceMap { get; init; }
    public bool HasSourceMap => !string.IsNullOrWhiteSpace(SourceMapUrl);
    public string DisplayName => string.IsNullOrWhiteSpace(Url)
        ? $"(anonymous script {ScriptId})"
        : Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.LocalPath)
            ? Path.GetFileName(uri.LocalPath)
            : Path.GetFileName(Url.Replace('\\', '/'));
    public string LocationDisplay => $"{DisplayName}:{StartLine + 1}";
    public override string ToString() => DisplayName;
}

public sealed class V8CallFrameModel
{
    public string CallFrameId { get; init; } = "";
    public string FunctionName { get; init; } = "";
    public string Url { get; init; } = "";
    public string ScriptId { get; init; } = "";
    public int LineNumber { get; init; }
    public int ColumnNumber { get; init; }
    public IReadOnlyList<V8ScopeModel> ScopeChain { get; init; } = Array.Empty<V8ScopeModel>();
    public string DisplayName => $"{(string.IsNullOrWhiteSpace(FunctionName) ? "(anonymous)" : FunctionName)} ({GetFileName()}:{LineNumber + 1}:{ColumnNumber + 1})";

    private string GetFileName()
    {
        if (string.IsNullOrWhiteSpace(Url)) return $"script {ScriptId}";
        return Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.LocalPath)
            ? Path.GetFileName(uri.LocalPath)
            : Path.GetFileName(Url.Replace('\\', '/'));
    }

    public override string ToString() => DisplayName;
}

public sealed class V8ScopeModel
{
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string ObjectId { get; init; } = "";
    public string Description { get; init; } = "";
    public ObservableCollection<V8PropertyModel> Properties { get; } = new();
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Type : $"{Type}: {Name}";
    public override string ToString() => DisplayName;
}

public sealed class V8PropertyModel
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Subtype { get; init; } = "";
    public string Value { get; init; } = "";
    public string ObjectId { get; init; } = "";
    public bool Writable { get; init; }
    public bool Enumerable { get; init; }
    public bool Configurable { get; init; }
    public bool IsExpandable => !string.IsNullOrWhiteSpace(ObjectId);
}

public sealed class V8BreakpointModel
{
    public string BreakpointId { get; init; } = "";
    public string ScriptId { get; init; } = "";
    public string Url { get; init; } = "";
    public int LineNumber { get; init; }
    public int ColumnNumber { get; init; }
    public int? DisplayLineNumber { get; init; }
    public string Condition { get; init; } = "";
    public bool IsResolved { get; set; }
    public string DisplayName => $"{GetFileName()}:{(DisplayLineNumber ?? LineNumber) + 1}:{ColumnNumber + 1}{(string.IsNullOrWhiteSpace(Condition) ? "" : $" if {Condition}")}";

    private string GetFileName()
    {
        if (string.IsNullOrWhiteSpace(Url)) return $"script {ScriptId}";
        return Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.LocalPath)
            ? Path.GetFileName(uri.LocalPath)
            : Path.GetFileName(Url.Replace('\\', '/'));
    }

    public override string ToString() => DisplayName;
}

public sealed class V8WatchExpressionModel : ViewModelBase
{
    private string _value = "Not available";

    public required string Expression { get; init; }

    public string Value
    {
        get => _value;
        set => RaiseAndSetIfChanged(ref _value, value);
    }

    public override string ToString() => $"{Expression} = {Value}";
}
