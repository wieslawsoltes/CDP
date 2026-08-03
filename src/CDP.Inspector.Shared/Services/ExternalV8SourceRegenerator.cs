using System.Diagnostics;
using System.Text.Json.Nodes;
using Chrome.DevTools.Protocol.Inspector;

namespace CdpInspectorApp.Services;

/// <summary>
/// Configuration for a source compiler that implements the CDP external-regenerator JSON
/// protocol over standard input and output.
/// </summary>
public sealed record ExternalV8SourceRegeneratorOptions(
    string Name,
    string Executable,
    IReadOnlyList<string> Extensions)
{
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Invokes an explicitly configured compiler adapter. The child process receives one JSON
/// request on stdin and returns one JSON result on stdout, allowing CoffeeScript, Vue, Svelte,
/// Reason, Babel, SWC, or a host-specific build pipeline to participate in V8 live edit.
/// </summary>
public sealed class ExternalV8SourceRegenerator : IV8SourceRegenerator
{
    public const int ProtocolVersion = 1;
    private readonly ExternalV8SourceRegeneratorOptions _options;
    private readonly HashSet<string> _extensions;

    public ExternalV8SourceRegenerator(ExternalV8SourceRegeneratorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.Name))
            throw new ArgumentException("An external regenerator name is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Executable))
            throw new ArgumentException("An external regenerator executable is required.", nameof(options));
        if (options.Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "The external regenerator timeout must be positive.");
        _extensions = options.Extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_extensions.Count == 0)
            throw new ArgumentException("At least one source extension is required.", nameof(options));
    }

    public string Name => _options.Name;

    public bool CanRegenerate(V8SourceRegenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _extensions.Contains("*") || _extensions.Contains(GetSourceExtension(request.SourceUrl));
    }

    public async ValueTask<V8SourceRegenerationResult> RegenerateAsync(
        V8SourceRegenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanRegenerate(request))
        {
            return V8SourceRegenerationResult.Failed(
                $"{Name} does not support '{GetSourceExtension(request.SourceUrl)}' sources.");
        }

        var workingDirectory = ResolveWorkingDirectory(request.SourceUrl);
        if (!Directory.Exists(workingDirectory))
        {
            return V8SourceRegenerationResult.Failed(
                $"Configured working directory '{workingDirectory}' does not exist.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.Executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in _options.Arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return V8SourceRegenerationResult.Failed($"Unable to start {Name}.");
        }
        catch (Exception ex)
        {
            return V8SourceRegenerationResult.Failed($"Unable to start {Name}: {ex.Message}");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            var payload = CreateRequest(request).ToJsonString();
            await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var diagnostic = string.IsNullOrWhiteSpace(error) ? output : error;
                return V8SourceRegenerationResult.Failed(
                    $"{Name} exited with code {process.ExitCode}: {diagnostic.Trim()}");
            }
            return ParseResult(request, output, error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            return V8SourceRegenerationResult.Failed(
                $"{Name} exceeded its {_options.Timeout.TotalSeconds:n0}-second timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        catch (Exception ex)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            return V8SourceRegenerationResult.Failed($"{Name} failed: {ex.Message}");
        }
    }

    public static IReadOnlyList<ExternalV8SourceRegenerator> LoadManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var fullPath = Path.GetFullPath(manifestPath);
        var root = JsonNode.Parse(File.ReadAllText(fullPath)) ??
            throw new FormatException("The external-regenerator manifest is empty.");
        var entries = root as JsonArray ?? root["regenerators"] as JsonArray ??
            throw new FormatException("The manifest must be an array or contain a 'regenerators' array.");
        var manifestDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var result = new List<ExternalV8SourceRegenerator>();
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var name = RequiredString(entry, "name");
            var executable = RequiredString(entry, "executable");
            if (ContainsDirectorySeparator(executable) && !Path.IsPathRooted(executable))
                executable = Path.GetFullPath(Path.Combine(manifestDirectory, executable));
            var extensions = ReadStrings(entry["extensions"] as JsonArray);
            var arguments = ReadStrings(entry["arguments"] as JsonArray);
            var workingDirectory = entry["workingDirectory"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(workingDirectory) && !Path.IsPathRooted(workingDirectory))
                workingDirectory = Path.GetFullPath(Path.Combine(manifestDirectory, workingDirectory));
            var timeoutSeconds = entry["timeoutSeconds"]?.GetValue<int>() ?? 30;
            if (timeoutSeconds is < 1 or > 600)
                throw new FormatException($"Regenerator '{name}' timeoutSeconds must be between 1 and 600.");
            result.Add(new ExternalV8SourceRegenerator(new(
                name,
                executable,
                extensions)
            {
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            }));
        }
        if (result.Count == 0)
            throw new FormatException("The external-regenerator manifest contains no adapters.");
        return result;
    }

    private static JsonObject CreateRequest(V8SourceRegenerationRequest request) => new()
    {
        ["protocolVersion"] = ProtocolVersion,
        ["sourceIndex"] = request.SourceIndex,
        ["sourceUrl"] = request.SourceUrl,
        ["generatedUrl"] = request.GeneratedUrl,
        ["originalSource"] = request.OriginalSource,
        ["editedSource"] = request.EditedSource,
        ["generatedSource"] = request.GeneratedSource,
        ["originalRevision"] = request.OriginalRevision.Sha256,
        ["editedRevision"] = request.EditedRevision.Sha256,
        ["generatedRevision"] = request.GeneratedRevision.Sha256,
        ["sourceMap"] = request.SourceMap.ToJsonObject()
    };

    private V8SourceRegenerationResult ParseResult(
        V8SourceRegenerationRequest request,
        string output,
        string standardError)
    {
        JsonObject result;
        try
        {
            result = JsonNode.Parse(output) as JsonObject ??
                throw new FormatException("stdout did not contain a JSON object.");
        }
        catch (Exception ex)
        {
            var suffix = string.IsNullOrWhiteSpace(standardError) ? "" : $" stderr: {standardError.Trim()}";
            return V8SourceRegenerationResult.Failed($"{Name} returned invalid JSON: {ex.Message}{suffix}");
        }

        var protocolVersion = result["protocolVersion"]?.GetValue<int>() ?? ProtocolVersion;
        if (protocolVersion != ProtocolVersion)
            return V8SourceRegenerationResult.Failed(
                $"{Name} returned unsupported protocol version {protocolVersion}.");
        var success = result["success"]?.GetValue<bool>() ?? false;
        var message = result["message"]?.GetValue<string>() ?? "";
        if (!success)
            return V8SourceRegenerationResult.Failed(
                string.IsNullOrWhiteSpace(message) ? $"{Name} rejected regeneration." : message);
        var generatedSource = result["generatedSource"]?.GetValue<string>();
        if (generatedSource is null)
            return V8SourceRegenerationResult.Failed($"{Name} did not return generatedSource.");
        var sourceMapNode = result["sourceMap"];
        if (sourceMapNode is null)
            return V8SourceRegenerationResult.Failed($"{Name} did not return sourceMap.");

        try
        {
            var sourceMapJson = sourceMapNode is JsonValue sourceMapValue &&
                sourceMapValue.TryGetValue<string>(out var encodedMap)
                ? encodedMap
                : sourceMapNode.ToJsonString();
            var mapUri = Uri.TryCreate(request.GeneratedUrl, UriKind.Absolute, out var generatedUri)
                ? generatedUri
                : null;
            var sourceMap = V8SourceMap.Parse(sourceMapJson, mapUri);
            var editedIndex = FindEditedSource(sourceMap, request.EditedSource, result["sourceIndex"]);
            if (editedIndex < 0)
                return V8SourceRegenerationResult.Failed(
                    $"{Name} source map does not contain the edited source in sourcesContent.");
            if (request.SourceIndex >= sourceMap.Sources.Count)
                return V8SourceRegenerationResult.Failed(
                    $"{Name} source map cannot preserve original source index {request.SourceIndex}.");
            sourceMap = sourceMap.RemapSourceIndex(
                editedIndex,
                request.SourceIndex,
                request.SourceMap.Sources[request.SourceIndex],
                request.EditedSource);
            return V8SourceRegenerationResult.Regenerated(
                generatedSource,
                sourceMap,
                string.IsNullOrWhiteSpace(message)
                    ? $"Source regenerated with {Name}; validating with V8..."
                    : message);
        }
        catch (Exception ex)
        {
            return V8SourceRegenerationResult.Failed($"{Name} returned an invalid source map: {ex.Message}");
        }
    }

    private string ResolveWorkingDirectory(string sourceUrl)
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
            return Path.GetFullPath(_options.WorkingDirectory);
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) && sourceUri.IsFile)
            return Path.GetDirectoryName(sourceUri.LocalPath) ?? Environment.CurrentDirectory;
        return Environment.CurrentDirectory;
    }

    private static int FindEditedSource(V8SourceMap sourceMap, string editedSource, JsonNode? hintedIndex)
    {
        if (hintedIndex is JsonValue value && value.TryGetValue<int>(out var hintedSourceIndex) &&
            hintedSourceIndex >= 0 && hintedSourceIndex < sourceMap.SourcesContent.Count &&
            string.Equals(sourceMap.SourcesContent[hintedSourceIndex], editedSource, StringComparison.Ordinal))
            return hintedSourceIndex;
        for (var index = 0; index < sourceMap.SourcesContent.Count; index++)
        {
            if (string.Equals(sourceMap.SourcesContent[index], editedSource, StringComparison.Ordinal)) return index;
        }
        return -1;
    }

    private static string GetSourceExtension(string sourceUrl)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return NormalizeExtension(Path.GetExtension(uri.AbsolutePath));
        var queryIndex = sourceUrl.IndexOfAny(['?', '#']);
        return NormalizeExtension(Path.GetExtension(queryIndex >= 0 ? sourceUrl[..queryIndex] : sourceUrl));
    }

    private static string NormalizeExtension(string extension)
    {
        extension = extension.Trim();
        return extension == "*" || extension.StartsWith('.') ? extension : "." + extension;
    }

    private static bool ContainsDirectorySeparator(string path) =>
        path.Contains(Path.DirectorySeparatorChar) || path.Contains(Path.AltDirectorySeparatorChar);

    private static string RequiredString(JsonObject entry, string propertyName) =>
        entry[propertyName]?.GetValue<string>() is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"External regenerator '{propertyName}' is required.");

    private static IReadOnlyList<string> ReadStrings(JsonArray? array) =>
        array?.Select(value => value?.GetValue<string>() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray() ?? [];
}

/// <summary>Builds the Inspector's default source-regenerator chain.</summary>
public static class V8SourceRegeneratorCatalog
{
    public const string ManifestEnvironmentVariable = "CDP_V8_SOURCE_REGENERATORS";

    public static IReadOnlyList<IV8SourceRegenerator> CreateDefault()
    {
        var result = new List<IV8SourceRegenerator> { new EsbuildV8SourceRegenerator() };
        var configured = Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured)) return result;
        foreach (var manifestPath in configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                result.AddRange(ExternalV8SourceRegenerator.LoadManifest(manifestPath));
            }
            catch (Exception ex)
            {
                result.Add(new FailedManifestRegenerator(manifestPath, ex.Message));
            }
        }
        return result;
    }

    private sealed class FailedManifestRegenerator(string path, string diagnostic) : IV8SourceRegenerator
    {
        public string Name => $"external manifest '{Path.GetFileName(path)}'";
        public bool CanRegenerate(V8SourceRegenerationRequest request) => true;
        public ValueTask<V8SourceRegenerationResult> RegenerateAsync(
            V8SourceRegenerationRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(V8SourceRegenerationResult.Failed(diagnostic));
    }
}
