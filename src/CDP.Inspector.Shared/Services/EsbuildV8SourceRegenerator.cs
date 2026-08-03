using System.Diagnostics;
using System.Text.Json.Nodes;
using Chrome.DevTools.Protocol.Inspector;

namespace CdpInspectorApp.Services;

/// <summary>
/// Regenerates single-source TypeScript and JavaScript scripts with esbuild. The executable
/// can be supplied explicitly, through CDP_ESBUILD_PATH, a project-local node_modules/.bin,
/// or PATH.
/// </summary>
public sealed class EsbuildV8SourceRegenerator : IV8SourceRegenerator
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx", ".mts", ".cts"
    };

    private readonly string? _executablePath;

    public EsbuildV8SourceRegenerator(string? executablePath = null)
    {
        _executablePath = executablePath;
    }

    public string Name => "esbuild";

    public bool CanRegenerate(V8SourceRegenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.SourceIndex == 0 && request.SourceMap.Sources.Count == 1 &&
            SupportedExtensions.Contains(GetSourceExtension(request.SourceUrl));
    }

    public async ValueTask<V8SourceRegenerationResult> RegenerateAsync(
        V8SourceRegenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanRegenerate(request))
        {
            return V8SourceRegenerationResult.Failed(
                "esbuild live edit currently requires one JS/TS source in the source map.");
        }

        var sourcePath = GetLocalSourcePath(request.SourceUrl);
        var searchDirectory = sourcePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(sourcePath);
        var executable = ResolveExecutable(searchDirectory);
        if (executable is null)
        {
            return V8SourceRegenerationResult.Failed(
                "esbuild was not found. Install it in the project, put it on PATH, or set CDP_ESBUILD_PATH.");
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"cdp-v8-regenerate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var extension = NormalizeInputExtension(GetSourceExtension(request.SourceUrl));
            var inputPath = Path.Combine(temporaryDirectory, "source" + extension);
            var outputPath = Path.Combine(temporaryDirectory, "generated.js");
            await File.WriteAllTextAsync(inputPath, request.EditedSource, cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = searchDirectory is not null && Directory.Exists(searchDirectory)
                    ? searchDirectory
                    : Environment.CurrentDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add($"--outfile={outputPath}");
            startInfo.ArgumentList.Add("--sourcemap=external");
            startInfo.ArgumentList.Add("--sources-content=true");
            startInfo.ArgumentList.Add("--charset=utf8");
            startInfo.ArgumentList.Add("--log-level=warning");

            var tsconfig = FindUpward(searchDirectory, "tsconfig.json");
            if (tsconfig is not null) startInfo.ArgumentList.Add($"--tsconfig={tsconfig}");
            var generatedExtension = GetSourceExtension(request.GeneratedUrl);
            if (generatedExtension.Equals(".cjs", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("--format=cjs");
            }
            else if (generatedExtension.Equals(".mjs", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("--format=esm");
            }

            using var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start()) return V8SourceRegenerationResult.Failed("Unable to start esbuild.");
            }
            catch (Exception ex)
            {
                return V8SourceRegenerationResult.Failed($"Unable to start esbuild: {ex.Message}");
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw;
            }
            var output = await standardOutput.ConfigureAwait(false);
            var error = await standardError.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var diagnostic = string.IsNullOrWhiteSpace(error) ? output : error;
                return V8SourceRegenerationResult.Failed(diagnostic.Trim());
            }

            var sourceMapPath = outputPath + ".map";
            if (!File.Exists(outputPath) || !File.Exists(sourceMapPath))
            {
                return V8SourceRegenerationResult.Failed("esbuild did not produce JavaScript and a source map.");
            }

            var generatedSource = await File.ReadAllTextAsync(outputPath, cancellationToken).ConfigureAwait(false);
            var sourceMapJson = await File.ReadAllTextAsync(sourceMapPath, cancellationToken).ConfigureAwait(false);
            var sourceMapRoot = JsonNode.Parse(sourceMapJson) as JsonObject;
            if (sourceMapRoot?["sources"] is not JsonArray sources || sources.Count != 1)
            {
                return V8SourceRegenerationResult.Failed("esbuild returned an incompatible source map.");
            }

            sources[0] = request.SourceMap.Sources[request.SourceIndex];
            sourceMapRoot["sourcesContent"] = new JsonArray(JsonValue.Create(request.EditedSource));
            sourceMapRoot["file"] = request.SourceMap.File;
            var mapUri = Uri.TryCreate(request.GeneratedUrl, UriKind.Absolute, out var generatedUri)
                ? generatedUri
                : null;
            var regeneratedMap = V8SourceMap.Parse(sourceMapRoot.ToJsonString(), mapUri);
            return V8SourceRegenerationResult.Regenerated(
                generatedSource,
                regeneratedMap,
                "Source regenerated with esbuild; validating with V8...");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return V8SourceRegenerationResult.Failed(ex.Message);
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
                // The OS can remove an orphaned temporary directory later.
            }
        }
    }

    private string? ResolveExecutable(string? startDirectory)
    {
        if (!string.IsNullOrWhiteSpace(_executablePath))
        {
            return File.Exists(_executablePath) ? Path.GetFullPath(_executablePath) : null;
        }

        var configured = Environment.GetEnvironmentVariable("CDP_ESBUILD_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return Path.GetFullPath(configured);

        var executableName = OperatingSystem.IsWindows() ? "esbuild.cmd" : "esbuild";
        for (var directory = startDirectory; !string.IsNullOrWhiteSpace(directory); directory = Directory.GetParent(directory)?.FullName)
        {
            var candidate = Path.Combine(directory, "node_modules", ".bin", executableName);
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var candidate = Path.Combine(path, executableName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindUpward(string? startDirectory, string fileName)
    {
        for (var directory = startDirectory; !string.IsNullOrWhiteSpace(directory); directory = Directory.GetParent(directory)?.FullName)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? GetLocalSourcePath(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || !uri.IsFile) return null;
        return uri.LocalPath;
    }

    private static string GetSourceExtension(string sourceUrl)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)) return Path.GetExtension(uri.AbsolutePath);
        var queryIndex = sourceUrl.IndexOfAny(['?', '#']);
        return Path.GetExtension(queryIndex >= 0 ? sourceUrl[..queryIndex] : sourceUrl);
    }

    private static string NormalizeInputExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".mts" or ".cts" => ".ts",
        ".mjs" or ".cjs" => ".js",
        _ => extension
    };
}
