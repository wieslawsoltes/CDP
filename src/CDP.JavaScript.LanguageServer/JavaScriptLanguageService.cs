using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Jint;

namespace CDP.JavaScript.LanguageServer;

public sealed record JavaScriptTextSpan(int Start, int Length);

public sealed record JavaScriptCompletion(
    string Name,
    string Kind,
    string KindModifiers,
    string SortText,
    string InsertText,
    string? Source,
    JavaScriptTextSpan? ReplacementSpan);

public sealed record JavaScriptQuickInfo(
    string Kind,
    string KindModifiers,
    JavaScriptTextSpan? TextSpan,
    string DisplayText,
    string Documentation);

public sealed record JavaScriptDiagnostic(
    int Code,
    int Category,
    int Start,
    int Length,
    string Message,
    string Source);

public sealed record JavaScriptDocumentSpan(
    string FileName,
    JavaScriptTextSpan TextSpan,
    JavaScriptTextSpan? ContextSpan,
    bool IsWriteAccess = false,
    bool IsDefinition = false);

public sealed record JavaScriptTextChange(int Start, int Length, string NewText);

public sealed record JavaScriptRenameResult(
    bool CanRename,
    string? Error,
    string? DisplayName,
    string? FullDisplayName,
    string? Kind,
    JavaScriptTextSpan? TriggerSpan,
    List<JavaScriptDocumentSpan> Locations);

public sealed record JavaScriptProjectDocument(string FileName, string Text);

public sealed class JavaScriptLanguageService
{
    private const string ServicesResource =
        "CDP.JavaScript.LanguageServer.TypeScript.typescriptServices.js";
    private const string HostResource =
        "CDP.JavaScript.LanguageServer.TypeScript.host.js";
    private const string LibraryPrefix =
        "CDP.JavaScript.LanguageServer.TypeScript.lib.";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _documents =
        new(StringComparer.Ordinal);
    private Engine? _engine;

    public string TypeScriptVersion { get; private set; } = "not loaded";

    public async Task OpenDocumentAsync(
        string fileName,
        string text,
        string? projectRoot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(text);
        var normalized = NormalizeFileName(fileName);
        _documents[normalized] = text;
        await InvokeAsync(
            engine => engine.Invoke(
                "__cdpTsOpen",
                normalized,
                text,
                NormalizeRoot(projectRoot, normalized)).AsString(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task OpenProjectAsync(
        IEnumerable<JavaScriptProjectDocument> documents,
        string? projectRoot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        foreach (var document in documents)
        {
            await OpenDocumentAsync(
                document.FileName,
                document.Text,
                projectRoot,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CloseDocumentAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeFileName(fileName);
        _documents.TryRemove(normalized, out _);
        await InvokeAsync(
            engine => engine.Invoke("__cdpTsClose", normalized).ToString(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JavaScriptCompletion>> GetCompletionsAsync(
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken = default) =>
        await InvokeJsonAsync<List<JavaScriptCompletion>>(
            "__cdpTsCompletions", fileName, line, column, cancellationToken)
            .ConfigureAwait(false);

    public Task<JavaScriptQuickInfo?> GetQuickInfoAsync(
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken = default) =>
        InvokeJsonAsync<JavaScriptQuickInfo?>(
            "__cdpTsHover", fileName, line, column, cancellationToken);

    public async Task<IReadOnlyList<JavaScriptDiagnostic>> GetDiagnosticsAsync(
        string fileName,
        CancellationToken cancellationToken = default) =>
        await InvokeJsonAsync<List<JavaScriptDiagnostic>>(
            "__cdpTsDiagnostics", fileName, cancellationToken).ConfigureAwait(false);

    public Task<JsonElement?> GetSignatureHelpAsync(
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken = default) =>
        InvokeJsonAsync<JsonElement?>(
            "__cdpTsSignatureHelp", fileName, line, column, cancellationToken);

    public async Task<IReadOnlyList<JavaScriptDocumentSpan>> GetDefinitionsAsync(
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken = default) =>
        await InvokeJsonAsync<List<JavaScriptDocumentSpan>>(
            "__cdpTsDefinitions", fileName, line, column, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<JavaScriptDocumentSpan>> GetReferencesAsync(
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken = default) =>
        await InvokeJsonAsync<List<JavaScriptDocumentSpan>>(
            "__cdpTsReferences", fileName, line, column, cancellationToken)
            .ConfigureAwait(false);

    public Task<JavaScriptRenameResult> GetRenameLocationsAsync(
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken = default) =>
        InvokeJsonAsync<JavaScriptRenameResult>(
            "__cdpTsRename", fileName, line, column, cancellationToken);

    public Task<JsonElement?> GetDocumentSymbolsAsync(
        string fileName,
        CancellationToken cancellationToken = default) =>
        InvokeJsonAsync<JsonElement?>(
            "__cdpTsSymbols", fileName, cancellationToken);

    public async Task<IReadOnlyList<int>> GetSemanticClassificationsAsync(
        string fileName,
        CancellationToken cancellationToken = default) =>
        await InvokeJsonAsync<List<int>>(
            "__cdpTsSemanticTokens", fileName, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<JavaScriptTextChange>> GetFormattingEditsAsync(
        string fileName,
        int tabSize = 4,
        bool insertSpaces = true,
        CancellationToken cancellationToken = default) =>
        await InvokeJsonAsync<List<JavaScriptTextChange>>(
            "__cdpTsFormat", fileName, tabSize, insertSpaces, cancellationToken)
            .ConfigureAwait(false);

    public (int Line, int Column) GetLineColumn(string fileName, int offset)
    {
        var normalized = NormalizeFileName(fileName);
        if (!_documents.TryGetValue(normalized, out var text)) return (1, 1);
        offset = Math.Clamp(offset, 0, text.Length);
        var line = 1;
        var lineStart = 0;
        for (var index = 0; index < offset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }
        return (line, offset - lineStart + 1);
    }

    private async Task<T> InvokeJsonAsync<T>(
        string function,
        string fileName,
        CancellationToken cancellationToken)
    {
        var json = await InvokeAsync(
            engine => engine.Invoke(function, NormalizeFileName(fileName)).AsString(),
            cancellationToken).ConfigureAwait(false);
        return Deserialize<T>(json);
    }

    private async Task<T> InvokeJsonAsync<T>(
        string function,
        string fileName,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        var json = await InvokeAsync(
            engine => engine.Invoke(
                function,
                NormalizeFileName(fileName),
                Math.Max(1, line),
                Math.Max(1, column)).AsString(),
            cancellationToken).ConfigureAwait(false);
        return Deserialize<T>(json);
    }

    private async Task<T> InvokeJsonAsync<T>(
        string function,
        string fileName,
        int tabSize,
        bool insertSpaces,
        CancellationToken cancellationToken)
    {
        var json = await InvokeAsync(
            engine => engine.Invoke(
                function,
                NormalizeFileName(fileName),
                tabSize,
                insertSpaces).AsString(),
            cancellationToken).ConfigureAwait(false);
        return Deserialize<T>(json);
    }

    private async Task<T> InvokeAsync<T>(
        Func<Engine, T> action,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => action(EnsureEngine()),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Engine EnsureEngine()
    {
        if (_engine is not null) return _engine;
        var assembly = typeof(JavaScriptLanguageService).Assembly;
        var engine = new Engine(options =>
            options.TimeoutInterval(TimeSpan.FromSeconds(45)));
        engine.Execute(ReadResource(assembly, ServicesResource));
        engine.Execute(ReadResource(assembly, HostResource));

        var libraries = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(LibraryPrefix, StringComparison.Ordinal))
            .ToDictionary(
                name => name["CDP.JavaScript.LanguageServer.TypeScript.".Length..],
                name => NormalizeLibrarySource(ReadResource(assembly, name)),
                StringComparer.Ordinal);
        var serialized = JsonSerializer.Serialize(
            libraries,
            JavaScriptLanguageServiceJsonContext.Default.DictionaryStringString);
        TypeScriptVersion = engine.Invoke("__cdpTsOpenLibraries", serialized).AsString();
        _engine = engine;
        return engine;
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded TypeScript resource '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string NormalizeLibrarySource(string source)
    {
        // Jint and TypeScript 5.9 otherwise combine compact negative literal
        // types (for example `[-1, 0]`) into a negative numeric token. A space
        // after the unary minus preserves declaration semantics while forcing
        // the token shape TypeScript's parser expects. Do not rewrite exponent
        // notation such as 1e-3.
        StringBuilder? normalized = null;
        var segmentStart = 0;
        for (var index = 0; index < source.Length - 1; index++)
        {
            if (source[index] != '-' || !char.IsAsciiDigit(source[index + 1])) continue;
            if (index > 0 && (char.IsAsciiLetterOrDigit(source[index - 1]) || source[index - 1] == '_'))
            {
                continue;
            }

            normalized ??= new StringBuilder(source.Length + 8);
            normalized.Append(source, segmentStart, index - segmentStart + 1);
            normalized.Append(' ');
            segmentStart = index + 1;
        }

        if (normalized is not null)
        {
            normalized.Append(source, segmentStart, source.Length - segmentStart);
            source = normalized.ToString();
        }

        // TypeScript's scanner converts this Web IDL sentinel through a
        // 32-bit bitwise path. Jint correctly exposes that result as -1,
        // which then violates TypeScript's negative-literal AST invariant.
        // Decimal spelling keeps the declared 32-bit unsigned value intact.
        return source.Replace("0xFFFFFFFF", "4294967295", StringComparison.Ordinal);
    }

    private static T Deserialize<T>(string json)
    {
        var typeInfo = JavaScriptLanguageServiceJsonContext.Default.GetTypeInfo(typeof(T))
            as JsonTypeInfo<T>
            ?? throw new InvalidOperationException(
                $"No JSON metadata is registered for TypeScript result '{typeof(T)}'.");
        var result = JsonSerializer.Deserialize(json, typeInfo);
        if (result is not null || !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null)
        {
            return result!;
        }

        throw new InvalidOperationException(
            "The TypeScript language service returned an empty response.");
    }

    private static string NormalizeRoot(string? projectRoot, string normalizedFileName)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot)) return NormalizeFileName(projectRoot);
        var slash = normalizedFileName.LastIndexOf('/');
        return slash <= 0 ? "/workspace" : normalizedFileName[..slash];
    }

    private static string NormalizeFileName(string fileName)
    {
        if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile) fileName = Uri.UnescapeDataString(uri.AbsolutePath);
            else fileName = $"/__cdp_virtual__/{uri.Host}{uri.AbsolutePath}";
        }
        var value = fileName.Replace('\\', '/');
        if (!value.StartsWith('/')) value = "/workspace/" + value.TrimStart('/');
        var parts = new List<string>();
        foreach (var part in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }
        return "/" + string.Join('/', parts);
    }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<JavaScriptCompletion>))]
[JsonSerializable(typeof(JavaScriptQuickInfo))]
[JsonSerializable(typeof(JsonElement?))]
[JsonSerializable(typeof(List<JavaScriptDiagnostic>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<JavaScriptDocumentSpan>))]
[JsonSerializable(typeof(JavaScriptRenameResult))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<JavaScriptTextChange>))]
internal sealed partial class JavaScriptLanguageServiceJsonContext : JsonSerializerContext;
