namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>
/// Regenerates a V8 script and its source map after an original source file was edited.
/// Implementations can wrap TypeScript, Babel, esbuild, SWC, or another language compiler.
/// </summary>
public interface IV8SourceRegenerator
{
    /// <summary>A short name shown in mutation diagnostics.</summary>
    string Name { get; }

    /// <summary>Returns whether this adapter supports the edited source and source map.</summary>
    bool CanRegenerate(V8SourceRegenerationRequest request);

    /// <summary>Compiles the edited source into JavaScript and a replacement source map.</summary>
    ValueTask<V8SourceRegenerationResult> RegenerateAsync(
        V8SourceRegenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Inputs supplied to a compiler-backed V8 source regenerator.</summary>
public sealed record V8SourceRegenerationRequest(
    V8SourceMap SourceMap,
    int SourceIndex,
    string SourceUrl,
    string GeneratedUrl,
    string OriginalSource,
    string EditedSource,
    string GeneratedSource);

/// <summary>JavaScript and source-map output produced by a source regenerator.</summary>
public sealed record V8SourceRegenerationResult(
    bool Success,
    string Message,
    string GeneratedSource,
    V8SourceMap? SourceMap)
{
    public static V8SourceRegenerationResult Regenerated(
        string generatedSource,
        V8SourceMap sourceMap,
        string message = "Source regenerated.")
    {
        ArgumentNullException.ThrowIfNull(generatedSource);
        ArgumentNullException.ThrowIfNull(sourceMap);
        return new(true, message, generatedSource, sourceMap);
    }

    public static V8SourceRegenerationResult Failed(string message) =>
        new(false, message, "", null);
}
