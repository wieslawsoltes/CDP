using System.Collections.ObjectModel;
using Avalonia.Threading;
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
    public string ScriptLanguage { get; init; } = "";
    public string BuildId { get; init; } = "";
    public int CodeOffset { get; init; }
    public string EmbedderName { get; init; } = "";
    public bool IsLiveEdit { get; init; }
    public IReadOnlyList<V8DebugSymbolModel> DebugSymbols { get; init; } = Array.Empty<V8DebugSymbolModel>();
    public bool IsOriginalSource { get; init; }
    public string GeneratedScriptId { get; init; } = "";
    public string GeneratedUrl { get; init; } = "";
    public int SourceIndex { get; init; } = -1;
    public string? SourceContent { get; set; }
    public V8SourceMap? SourceMap { get; set; }
    public V8WasmDisassembly? WasmDisassembly { get; set; }
    public int WasmBytecodeSize { get; set; }
    public bool IsIgnoredSource { get; init; }
    public bool HasSourceMap => !string.IsNullOrWhiteSpace(SourceMapUrl);
    public bool IsWebAssembly => ScriptLanguage.Equals("WebAssembly", StringComparison.OrdinalIgnoreCase);
    public string LanguageBadge => GetLanguageBadge();
    public string DisplayName => string.IsNullOrWhiteSpace(Url)
        ? $"(anonymous script {ScriptId})"
        : Uri.TryCreate(Url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.LocalPath)
            ? Path.GetFileName(uri.LocalPath)
            : Path.GetFileName(Url.Replace('\\', '/'));
    public string LocationDisplay => $"{DisplayName}:{StartLine + 1}{(IsIgnoredSource ? " · ignored" : "")}";
    public string DetailDisplay => string.Join(" · ", new[]
    {
        IsWebAssembly ? "WebAssembly" : IsOriginalSource ? "source mapped" : "JavaScript",
        DebugSymbols.Count == 0 ? null : string.Join(", ", DebugSymbols.Select(symbol => symbol.Type)),
        string.IsNullOrWhiteSpace(BuildId) ? null : $"build {BuildId}",
        Url
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private string GetLanguageBadge()
    {
        if (IsWebAssembly) return "WASM";
        var extension = Path.GetExtension(Url).TrimStart('.').ToUpperInvariant();
        return extension switch
        {
            "MTS" or "CTS" => "TS",
            "MJS" or "CJS" => "JS",
            { Length: > 0 and <= 6 } => extension,
            _ => "JS"
        };
    }

    public override string ToString() => DisplayName;
}

public sealed class V8DebugSymbolModel
{
    public string Type { get; init; } = "";
    public string ExternalUrl { get; init; } = "";
}

public sealed class V8ExecutionContextModel : ViewModelBase
{
    private bool _isBlackboxed;

    public int Id { get; init; }
    public string UniqueId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Origin { get; init; } = "";
    public string Type { get; init; } = "";
    public bool IsDefault { get; init; }
    public bool CanBlackbox => !string.IsNullOrWhiteSpace(UniqueId);
    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : !string.IsNullOrWhiteSpace(Origin)
            ? Origin
            : $"Execution context {Id}";
    public string Detail => string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(Type) ? null : Type,
            IsDefault ? "default" : null,
            $"id {Id}"
        }.Where(value => value is not null));

    public bool IsBlackboxed
    {
        get => _isBlackboxed;
        set => RaiseAndSetIfChanged(ref _isBlackboxed, value);
    }

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
    public bool HasReturnValue { get; init; }
    public string AsyncDescription { get; init; } = "";
    public IReadOnlyList<V8ScopeModel> ScopeChain { get; init; } = Array.Empty<V8ScopeModel>();
    public bool CanInspect => !string.IsNullOrWhiteSpace(CallFrameId) && !IsAsyncBoundary;
    public bool CanSetReturnValue => CanInspect && HasReturnValue;
    public bool IsWebAssembly => Url.StartsWith("wasm:", StringComparison.OrdinalIgnoreCase) ||
        Url.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase);
    public string DisplayName => IsAsyncBoundary
        ? $"— async: {(string.IsNullOrWhiteSpace(AsyncDescription) ? "continuation" : AsyncDescription)} —"
        : IsWebAssembly
            ? $"{(IsAsyncFrame ? "async · " : "")}{(string.IsNullOrWhiteSpace(FunctionName) ? "(anonymous)" : FunctionName)} ({GetFileName()}:+0x{ColumnNumber:x})"
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
    private bool _isExpanded;
    private Func<V8ScopeVariableModel, Task<IReadOnlyList<V8ScopeVariableModel>>>? _childrenLoader;
    private Task<IReadOnlyList<V8ScopeVariableModel>>? _childrenLoadTask;

    public string ScopeType { get; init; } = "";
    public int ScopeNumber { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Subtype { get; init; } = "";
    public string ObjectId { get; init; } = "";
    public bool Writable { get; init; }
    public bool IsScopeGroup { get; init; }
    public bool IsNested { get; init; }
    public bool IsAccessor { get; init; }
    public bool IsCircular { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsInternal { get; init; }
    public bool IsPlaceholder { get; init; }
    public bool IsDepthLimited { get; init; }
    public int Depth { get; init; }
    public int PauseGeneration { get; init; }
    public IReadOnlySet<string> AncestorObjectIds { get; init; } = new HashSet<string>();
    public ObservableCollection<V8ScopeVariableModel> Children { get; } = new();
    public bool IsExpandable => !IsPlaceholder && !IsAccessor && !IsCircular && !IsDepthLimited &&
        !string.IsNullOrWhiteSpace(ObjectId) && _childrenLoader is not null;
    public string DisplayName => IsScopeGroup ? Name : IsNested ? Name : $"[{ScopeType}] {Name}";
    public string TypeDisplay => IsAccessor
        ? "accessor"
        : string.IsNullOrWhiteSpace(Subtype) ? Type : Subtype;

    public string Value
    {
        get => _value;
        set => RaiseAndSetIfChanged(ref _value, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (RaiseAndSetIfChanged(ref _isExpanded, value) && value)
            {
                _ = EnsureChildrenLoadedAsync();
            }
        }
    }

    public void ConfigureChildrenLoader(Func<V8ScopeVariableModel, Task<IReadOnlyList<V8ScopeVariableModel>>> loader)
    {
        _childrenLoader = loader;
        if (Children.Count == 0)
        {
            Children.Add(new V8ScopeVariableModel
            {
                Name = "Loading…",
                Value = "",
                IsNested = true,
                IsPlaceholder = true,
                PauseGeneration = PauseGeneration
            });
        }
    }

    public IEnumerable<V8ScopeVariableModel> GetChildren()
        => Children;

    public Task EnsureChildrenLoadedAsync()
        => LoadChildrenForHierarchyAsync(CancellationToken.None);

    public Task<IReadOnlyList<V8ScopeVariableModel>> LoadChildrenForHierarchyAsync(CancellationToken cancellationToken)
    {
        if (!IsExpandable) return Task.FromResult<IReadOnlyList<V8ScopeVariableModel>>(Array.Empty<V8ScopeVariableModel>());
        return _childrenLoadTask ??= LoadChildrenAsync();
    }

    private async Task<IReadOnlyList<V8ScopeVariableModel>> LoadChildrenAsync()
    {
        try
        {
            var children = _childrenLoader is null
                ? Array.Empty<V8ScopeVariableModel>()
                : await _childrenLoader(this).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Children.Clear();
                foreach (var child in children) Children.Add(child);
            });
            return children;
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Children.Clear();
                Children.Add(new V8ScopeVariableModel
                {
                    Name = "Unable to load properties",
                    Value = ex.Message,
                    IsNested = true,
                    IsPlaceholder = true,
                    PauseGeneration = PauseGeneration
                });
            });
            return Children;
        }
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
    public string ScriptId { get; set; } = "";
    public string Url { get; init; } = "";
    public string BindingUrl { get; init; } = "";
    public bool IsWebAssembly { get; init; }
    public string BuildId { get; init; } = "";
    public string FunctionExpression { get; init; } = "";
    public string Instrumentation { get; init; } = "";
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
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
    public string DisplayName => Kind switch
    {
        V8BreakpointKinds.FunctionCall => $"Function: {FunctionExpression}{GetDetailSuffix()} [{Status}]",
        V8BreakpointKinds.Instrumentation => $"Instrumentation: {V8InstrumentationBreakpoints.GetDisplayName(Instrumentation)} [{Status}]",
        _ when IsWebAssembly => $"{GetFileName()}:+0x{ColumnNumber:x}{GetDetailSuffix()} [{Status}]",
        _ => $"{GetFileName()}:{(DisplayLineNumber ?? LineNumber) + 1}:{ColumnNumber + 1}{GetDetailSuffix()} [{Status}]"
    };

    private string GetDetailSuffix() => Kind switch
    {
        V8BreakpointKinds.Conditional when !string.IsNullOrWhiteSpace(Condition) => $" if {Condition}",
        V8BreakpointKinds.Logpoint when !string.IsNullOrWhiteSpace(LogMessage) => $" log {LogMessage}",
        V8BreakpointKinds.FunctionCall when !string.IsNullOrWhiteSpace(Condition) => $" if {Condition}",
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
    public const string FunctionCall = "Function call";
    public const string Instrumentation = "Instrumentation";

    public static string Normalize(string? value) => value switch
    {
        Conditional => Conditional,
        Logpoint => Logpoint,
        FunctionCall => FunctionCall,
        Instrumentation => Instrumentation,
        _ => Breakpoint
    };

    public static bool IsSourceLocation(string? value) => Normalize(value) != FunctionCall && Normalize(value) != Instrumentation;
}

public static class V8InstrumentationBreakpoints
{
    public const string BeforeScriptExecution = "beforeScriptExecution";
    public const string BeforeScriptWithSourceMapExecution = "beforeScriptWithSourceMapExecution";
    public const string BeforeScriptDisplayName = "Before script";
    public const string BeforeSourceMappedScriptDisplayName = "Before source-mapped script";

    public static string Normalize(string? value) => value switch
    {
        BeforeScriptWithSourceMapExecution or BeforeSourceMappedScriptDisplayName => BeforeScriptWithSourceMapExecution,
        _ => BeforeScriptExecution
    };

    public static string GetDisplayName(string? value) => Normalize(value) switch
    {
        BeforeScriptWithSourceMapExecution => BeforeSourceMappedScriptDisplayName,
        _ => BeforeScriptDisplayName
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
