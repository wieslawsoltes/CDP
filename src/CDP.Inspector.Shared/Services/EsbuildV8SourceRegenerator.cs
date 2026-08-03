using System.Diagnostics;
using System.Text.Json.Nodes;
using Chrome.DevTools.Protocol.Inspector;

namespace CdpInspectorApp.Services;

/// <summary>
/// Regenerates TypeScript and JavaScript scripts with esbuild. Single-source transforms use
/// the edited source directly. Multi-source entry maps are rebuilt as project-aware bundles,
/// resolving imports from the source directory without changing files in the workspace. The
/// executable can be supplied explicitly, through CDP_ESBUILD_PATH, a project-local
/// node_modules/.bin, or PATH.
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
        if (request.SourceIndex != 0 ||
            !SupportedExtensions.Contains(GetSourceExtension(request.SourceUrl)))
        {
            return false;
        }

        if (request.SourceMap.Sources.Count == 1) return true;
        var sourcePath = GetLocalSourcePath(request.SourceUrl);
        return sourcePath is not null && File.Exists(sourcePath);
    }

    public async ValueTask<V8SourceRegenerationResult> RegenerateAsync(
        V8SourceRegenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanRegenerate(request))
        {
            return V8SourceRegenerationResult.Failed(
                "esbuild project regeneration requires the first mapped JS/TS source and a local entry file for bundled maps.");
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
            var outputPath = Path.Combine(temporaryDirectory, "generated.js");
            var extension = NormalizeInputExtension(GetSourceExtension(request.SourceUrl));
            var isProjectBundle = request.SourceMap.Sources.Count > 1;
            var inputPath = Path.Combine(temporaryDirectory, "source" + extension);
            var sourceFileName = sourcePath is null
                ? "source" + extension
                : Path.GetFileName(sourcePath);
            if (!isProjectBundle)
            {
                await File.WriteAllTextAsync(inputPath, request.EditedSource, cancellationToken).ConfigureAwait(false);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = searchDirectory is not null && Directory.Exists(searchDirectory)
                    ? searchDirectory
                    : Environment.CurrentDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = isProjectBundle,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!isProjectBundle) startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add($"--outfile={outputPath}");
            startInfo.ArgumentList.Add("--sourcemap=external");
            startInfo.ArgumentList.Add("--sources-content=true");
            startInfo.ArgumentList.Add("--charset=utf8");
            startInfo.ArgumentList.Add("--log-level=warning");
            if (isProjectBundle)
            {
                startInfo.ArgumentList.Add("--bundle");
                startInfo.ArgumentList.Add($"--sourcefile={sourceFileName}");
                startInfo.ArgumentList.Add($"--loader={GetLoader(extension)}");
            }

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
            if (isProjectBundle && generatedExtension.Equals(".cjs", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("--platform=node");
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

            if (isProjectBundle)
            {
                await process.StandardInput.WriteAsync(request.EditedSource.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
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
            if (sourceMapRoot?["sources"] is not JsonArray sources || sources.Count == 0)
            {
                return V8SourceRegenerationResult.Failed("esbuild returned an incompatible source map.");
            }

            if (sourceMapRoot["sourcesContent"] is not JsonArray sourcesContent || sourcesContent.Count != sources.Count)
            {
                return V8SourceRegenerationResult.Failed("esbuild returned a source map without embedded source content.");
            }
            sourceMapRoot["file"] = request.SourceMap.File;
            var mapUri = Uri.TryCreate(request.GeneratedUrl, UriKind.Absolute, out var generatedUri)
                ? generatedUri
                : null;
            var regeneratedMap = V8SourceMap.Parse(sourceMapRoot.ToJsonString(), mapUri);
            var editedSourceIndex = FindEditedSourceIndex(regeneratedMap, request.EditedSource, sourceFileName);
            if (editedSourceIndex < 0)
            {
                return V8SourceRegenerationResult.Failed("esbuild source map does not identify the edited entry source.");
            }
            regeneratedMap = regeneratedMap.RemapSourceIndex(
                editedSourceIndex,
                request.SourceIndex,
                request.SourceMap.Sources[request.SourceIndex],
                request.EditedSource);
            return V8SourceRegenerationResult.Regenerated(
                generatedSource,
                regeneratedMap,
                isProjectBundle
                    ? "Project bundle regenerated with esbuild; validating with V8..."
                    : "Source regenerated with esbuild; validating with V8...");
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

    private static string GetLoader(string extension) => extension.ToLowerInvariant() switch
    {
        ".ts" => "ts",
        ".tsx" => "tsx",
        ".jsx" => "jsx",
        _ => "js"
    };

    private static int FindEditedSourceIndex(V8SourceMap sourceMap, string editedSource, string sourceFileName)
    {
        for (var index = 0; index < sourceMap.SourcesContent.Count; index++)
        {
            if (string.Equals(sourceMap.SourcesContent[index], editedSource, StringComparison.Ordinal)) return index;
        }
        for (var index = 0; index < sourceMap.Sources.Count; index++)
        {
            if (string.Equals(Path.GetFileName(sourceMap.Sources[index].Replace('\\', '/')), sourceFileName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }
}
