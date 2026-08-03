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
    public bool IsAsyncFrame { get; init; }
    public bool IsAsyncBoundary { get; init; }
    public string AsyncDescription { get; init; } = "";
    public IReadOnlyList<V8ScopeModel> ScopeChain { get; init; } = Array.Empty<V8ScopeModel>();
    public bool CanInspect => !string.IsNullOrWhiteSpace(CallFrameId) && !IsAsyncBoundary;
    public string DisplayName => IsAsyncBoundary
        ? $"— async: {(string.IsNullOrWhiteSpace(AsyncDescription) ? "continuation" : AsyncDescription)} —"
        : $"{(IsAsyncFrame ? "async · " : "")}{(string.IsNullOrWhiteSpace(FunctionName) ? "(anonymous)" : FunctionName)} ({GetFileName()}:{LineNumber + 1}:{ColumnNumber + 1})";

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
    public int Index { get; init; }
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string ObjectId { get; init; } = "";
    public string Description { get; init; } = "";
    public ObservableCollection<V8PropertyModel> Properties { get; } = new();
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Type : $"{Type}: {Name}";
    public override string ToString() => DisplayName;
}

public sealed class V8ScopeVariableModel : ViewModelBase
{
    private string _value = "undefined";

    public string ScopeType { get; init; } = "";
    public int ScopeNumber { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string ObjectId { get; init; } = "";
    public bool Writable { get; init; }
    public string DisplayName => $"[{ScopeType}] {Name}";

    public string Value
    {
        get => _value;
        set => RaiseAndSetIfChanged(ref _value, value);
    }
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

public sealed class V8BreakpointModel : ViewModelBase
{
    private string _breakpointId = "";
    private string _condition = "";
    private string _logMessage = "";
    private string _kind = V8BreakpointKinds.Breakpoint;
    private bool _isEnabled = true;
    private bool _isResolved;
    private int? _resolvedLineNumber;
    private int? _resolvedColumnNumber;

    public string Key { get; init; } = "";
    public string BreakpointId
    {
        get => _breakpointId;
        set
        {
            if (RaiseAndSetIfChanged(ref _breakpointId, value)) RaiseDisplayProperties();
        }
    }
    public string ScriptId { get; init; } = "";
    public string Url { get; init; } = "";
    public string BindingUrl { get; init; } = "";
    public int LineNumber { get; init; }
    public int ColumnNumber { get; init; }
    public int? DisplayLineNumber { get; init; }
    public string Condition
    {
        get => _condition;
        set
        {
            if (RaiseAndSetIfChanged(ref _condition, value)) RaiseDisplayProperties();
        }
    }
    public string LogMessage
    {
        get => _logMessage;
        set
        {
            if (RaiseAndSetIfChanged(ref _logMessage, value)) RaiseDisplayProperties();
        }
    }
    public string Kind
    {
        get => _kind;
        set
        {
            if (RaiseAndSetIfChanged(ref _kind, V8BreakpointKinds.Normalize(value))) RaiseDisplayProperties();
        }
    }
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (RaiseAndSetIfChanged(ref _isEnabled, value)) RaiseDisplayProperties();
        }
    }
    public bool IsResolved
    {
        get => _isResolved;
        set
        {
            if (RaiseAndSetIfChanged(ref _isResolved, value)) RaiseDisplayProperties();
        }
    }
    public int? ResolvedLineNumber
    {
        get => _resolvedLineNumber;
        set
        {
            if (RaiseAndSetIfChanged(ref _resolvedLineNumber, value)) RaiseDisplayProperties();
        }
    }
    public int? ResolvedColumnNumber
    {
        get => _resolvedColumnNumber;
        set
        {
            if (RaiseAndSetIfChanged(ref _resolvedColumnNumber, value)) RaiseDisplayProperties();
        }
    }

    public string Status => !IsEnabled ? "Disabled" : IsResolved ? "Resolved" : "Unbound";
    public string DisplayName => $"{GetFileName()}:{(DisplayLineNumber ?? LineNumber) + 1}:{ColumnNumber + 1}{GetDetailSuffix()} [{Status}]";

    private string GetDetailSuffix() => Kind switch
    {
        V8BreakpointKinds.Conditional when !string.IsNullOrWhiteSpace(Condition) => $" if {Condition}",
        V8BreakpointKinds.Logpoint when !string.IsNullOrWhiteSpace(LogMessage) => $" log {LogMessage}",
        _ => ""
    };

    private void RaiseDisplayProperties()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(DisplayName));
    }

    private string GetFileName()
    {
        if (string.IsNullOrWhiteSpace(Url)) return $"script {ScriptId}";
        return Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.LocalPath)
            ? Path.GetFileName(uri.LocalPath)
            : Path.GetFileName(Url.Replace('\\', '/'));
    }

    public override string ToString() => DisplayName;
}

public static class V8BreakpointKinds
{
    public const string Breakpoint = "Breakpoint";
    public const string Conditional = "Conditional";
    public const string Logpoint = "Logpoint";

    public static string Normalize(string? value) => value switch
    {
        Conditional => Conditional,
        Logpoint => Logpoint,
        _ => Breakpoint
    };
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
