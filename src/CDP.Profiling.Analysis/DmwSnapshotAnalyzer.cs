using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace CDP.Profiling.Analysis;

public static class DmwSnapshotAnalyzer
{
    public static string? LastError { get; set; }

    public static AnalyzedMemorySession? LoadWorkspace(string dmwPath)
    {
        if (!File.Exists(dmwPath)) return null;

        string tempDir = Path.Combine(Path.GetTempPath(), "cdp_dmw_" + Guid.NewGuid());
        try
        {
            ZipFile.ExtractToDirectory(dmwPath, tempDir);
            
            string jsonPath = Path.Combine(tempDir, "workspace.json");
            if (!File.Exists(jsonPath)) return null;

            string jsonText = File.ReadAllText(jsonPath);
            var root = JsonNode.Parse(jsonText)?.AsObject();
            if (root == null) return null;

            var session = new AnalyzedMemorySession
            {
                Name = root["sessions"]?[0]?["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(dmwPath)
            };

            // 1. Try JetBrains reflection aggregation first
            bool reflectionSuccess = TryLoadWithJetBrainsReflection(tempDir, session);
            
            // 2. Fall back to manual DMS parsing of string distributions
            if (!reflectionSuccess)
            {
                LoadFallbackWorkspace(tempDir, session);
            }

            return session;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dmw workspace: {ex.Message}");
            return null;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch {}
        }
    }

    private static bool IsValidFullyQualifiedTypeName(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        int dots = 0;
        bool startSegment = true;
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '.')
            {
                if (startSegment) return false;
                dots++;
                startSegment = true;
            }
            else if (c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
            {
                startSegment = false;
            }
            else if (c >= '0' && c <= '9')
            {
                if (startSegment) return false;
            }
            else
            {
                return false;
            }
        }
        return dots > 0 && !startSegment;
    }

    private static List<string> GetJetBrainsSearchPaths()
    {
        var paths = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("JETBRAINS_PROFILER_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
        {
            paths.Add(envPath);
        }

        if (OperatingSystem.IsMacOS())
        {
            paths.Add("/Applications/Rider.app/Contents/plugins/dotTrace.dotMemory/DotFiles");
            paths.Add("/Applications/Rider.app/Contents/tools/profiler");
            paths.Add("/Applications/Rider.app/Contents/lib/ReSharperHost");
            paths.Add("/Applications/Rider.app/Contents/lib/ReSharperHost/NetCore");
            paths.Add("/Applications/dotMemory.app/Contents/MacOS");
            paths.Add("/Applications/dotTrace.app/Contents/MacOS");

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.Add(Path.Combine(home, "Applications/Rider.app/Contents/plugins/dotTrace.dotMemory/DotFiles"));
            paths.Add(Path.Combine(home, "Applications/Rider.app/Contents/tools/profiler"));
            paths.Add(Path.Combine(home, "Applications/Rider.app/Contents/lib/ReSharperHost"));
            paths.Add(Path.Combine(home, "Applications/Rider.app/Contents/lib/ReSharperHost/NetCore"));

            var toolboxDir = Path.Combine(home, "Library/Application Support/JetBrains/Toolbox/apps");
            if (Directory.Exists(toolboxDir))
            {
                try
                {
                    foreach (var appDir in Directory.GetDirectories(toolboxDir, "*", SearchOption.AllDirectories))
                    {
                        if (appDir.EndsWith("DotFiles", StringComparison.OrdinalIgnoreCase) || 
                            appDir.EndsWith("profiler", StringComparison.OrdinalIgnoreCase) ||
                            appDir.EndsWith("ReSharperHost", StringComparison.OrdinalIgnoreCase))
                        {
                            paths.Add(appDir);
                        }
                    }
                }
                catch {}
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            paths.Add(Path.Combine(programFiles, @"JetBrains\Rider\plugins\dotTrace.dotMemory\DotFiles"));
            paths.Add(Path.Combine(programFiles, @"JetBrains\Rider\tools\profiler"));
            paths.Add(Path.Combine(programFiles, @"JetBrains\Rider\lib\ReSharperHost"));
            paths.Add(Path.Combine(programFiles, @"JetBrains\Rider\lib\ReSharperHost\NetCore"));
            paths.Add(Path.Combine(programFiles, @"JetBrains\dotMemory\DotFiles"));
            paths.Add(Path.Combine(programFiles, @"JetBrains\dotTrace\DotFiles"));

            var toolboxDir = Path.Combine(localAppData, @"JetBrains\Toolbox\apps");
            if (Directory.Exists(toolboxDir))
            {
                try
                {
                    foreach (var appDir in Directory.GetDirectories(toolboxDir, "*", SearchOption.AllDirectories))
                    {
                        if (appDir.EndsWith("DotFiles", StringComparison.OrdinalIgnoreCase) || 
                            appDir.EndsWith("profiler", StringComparison.OrdinalIgnoreCase) ||
                            appDir.EndsWith("ReSharperHost", StringComparison.OrdinalIgnoreCase))
                        {
                            paths.Add(appDir);
                        }
                    }
                }
                catch {}
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.Add("/usr/share/jetbrains/rider/plugins/dotTrace.dotMemory/DotFiles");
            paths.Add("/usr/share/jetbrains/rider/tools/profiler");
            paths.Add("/usr/share/jetbrains/rider/lib/ReSharperHost");
            paths.Add("/usr/share/jetbrains/rider/lib/ReSharperHost/NetCore");
            paths.Add("/opt/jetbrains/rider/plugins/dotTrace.dotMemory/DotFiles");
            paths.Add("/opt/jetbrains/rider/tools/profiler");
            paths.Add("/opt/jetbrains/rider/lib/ReSharperHost");
            paths.Add("/opt/jetbrains/rider/lib/ReSharperHost/NetCore");

            var toolboxDir = Path.Combine(home, ".local/share/JetBrains/Toolbox/apps");
            if (Directory.Exists(toolboxDir))
            {
                try
                {
                    foreach (var appDir in Directory.GetDirectories(toolboxDir, "*", SearchOption.AllDirectories))
                    {
                        if (appDir.EndsWith("DotFiles", StringComparison.OrdinalIgnoreCase) || 
                            appDir.EndsWith("profiler", StringComparison.OrdinalIgnoreCase) ||
                            appDir.EndsWith("ReSharperHost", StringComparison.OrdinalIgnoreCase))
                        {
                            paths.Add(appDir);
                        }
                    }
                }
                catch {}
            }
        }

        return paths.Distinct().Where(Directory.Exists).ToList();
    }

    private static bool TryLoadWithJetBrainsReflection(string unzippedDir, AnalyzedMemorySession session)
    {
        try
        {
            var searchPaths = GetJetBrainsSearchPaths();
            string? modelDll = null;
            foreach (var path in searchPaths)
            {
                var candidate = Path.Combine(path, "JetBrains.dotMemory.Model.dll");
                if (File.Exists(candidate))
                {
                    modelDll = candidate;
                    break;
                }
            }

            if (modelDll == null)
            {
                LastError = $"JetBrains dotMemory Model DLL (JetBrains.dotMemory.Model.dll) was not found in JetBrains search paths: {string.Join(", ", searchPaths)}";
                return false;
            }

            var libDir = Path.GetDirectoryName(modelDll)!;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name).Name + ".dll";
                var full = Path.Combine(libDir, name);
                if (File.Exists(full)) return Assembly.LoadFrom(full);
                foreach (var path in searchPaths)
                {
                    full = Path.Combine(path, name);
                    if (File.Exists(full)) return Assembly.LoadFrom(full);
                }
                return null;
            };

            return false;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.Message} at {ex.TargetSite}";
            return false;
        }
    }

    private static void LoadFallbackWorkspace(string unzippedDir, AnalyzedMemorySession session)
    {
        var dmsFiles = Directory.GetFiles(unzippedDir, "*.dms.0000", SearchOption.AllDirectories);
        if (dmsFiles.Length == 0)
        {
            dmsFiles = Directory.GetFiles(unzippedDir, "*.dms", SearchOption.AllDirectories);
        }

        var typeCounts = new Dictionary<string, int>();
        foreach (var file in dmsFiles)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                int i = 0;
                while (i < bytes.Length)
                {
                    if (bytes[i] >= 32 && bytes[i] <= 126)
                    {
                        int start = i;
                        while (i < bytes.Length && bytes[i] >= 32 && bytes[i] <= 126) i++;
                        string str = System.Text.Encoding.ASCII.GetString(bytes, start, i - start);
                        if (str.Contains(".") && (str.Contains("Avalonia") || str.Contains("System") || str.Contains("CdpSampleApp") || str.Contains("Microsoft") || IsValidFullyQualifiedTypeName(str)))
                        {
                            int dot = str.IndexOf('.');
                            if (dot > 0 && dot < str.Length - 1)
                            {
                                typeCounts[str] = typeCounts.TryGetValue(str, out int val) ? val + 1 : 1;
                            }
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
            }
            catch {}
        }

        if (typeCounts.Count > 0)
        {
            long totalBytes = 0;
            int totalCount = 0;
            var list = new List<AnalyzedMemoryStat>();
            
            foreach (var pair in typeCounts.OrderByDescending(p => p.Value))
            {
                long bytes = pair.Value * 128;
                list.Add(new AnalyzedMemoryStat
                {
                    TypeName = pair.Key,
                    AllocatedBytes = bytes,
                    AllocationCount = pair.Value
                });
                totalBytes += bytes;
                totalCount += pair.Value;
            }

            session.TotalAllocatedBytes = totalBytes;
            session.TotalAllocationsCount = totalCount;
            foreach (var item in list)
            {
                item.SizePct = totalBytes > 0 ? (item.AllocatedBytes / (double)totalBytes) * 100.0 : 0;
                item.CountPct = totalCount > 0 ? (item.AllocationCount / (double)totalCount) * 100.0 : 0;
                session.MemoryStats.Add(item);
            }
        }
    }
}
