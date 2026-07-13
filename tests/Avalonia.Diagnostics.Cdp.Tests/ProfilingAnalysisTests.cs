using System;
using System.IO;
using System.Reflection;
using Xunit;
using CDP.Profiling.Analysis;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ProfilingAnalysisTests
{
    [Fact]
    public void TestLoadDmwWithFallback()
    {
        // 1. Create a dummy .dmw zip file
        string tempZip = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.dmw");
        string tempSourceDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempSourceDir);

        try
        {
            // Write workspace.json
            string jsonContent = @"{
                ""sessions"": [
                    {
                        ""name"": ""My Custom Session""
                    }
                ]
            }";
            File.WriteAllText(Path.Combine(tempSourceDir, "workspace.json"), jsonContent);

            // Write a dummy .dms.0000 file with control type names
            string dummyDmsContent = "SomePrefix.Avalonia.Controls.Button.SomeSuffix.System.String.OtherText";
            File.WriteAllText(Path.Combine(tempSourceDir, "000.dms.0000"), dummyDmsContent);

            System.IO.Compression.ZipFile.CreateFromDirectory(tempSourceDir, tempZip);

            // 2. Load workspace
            var session = DmwSnapshotAnalyzer.LoadWorkspace(tempZip);
            
            Assert.NotNull(session);
            Assert.Equal("My Custom Session", session.Name);
            Assert.True(session.TotalAllocatedBytes > 0);
            Assert.True(session.TotalAllocationsCount > 0);
            Assert.NotEmpty(session.MemoryStats);

            // Check if our string scanner extracted the dummy types
            var textBlockStat = session.MemoryStats.FirstOrDefault(s => s.TypeName.Contains("Button"));
            var stringStat = session.MemoryStats.FirstOrDefault(s => s.TypeName.Contains("String"));

            Assert.NotNull(textBlockStat);
            Assert.NotNull(stringStat);
        }
        finally
        {
            try { File.Delete(tempZip); } catch {}
            try { Directory.Delete(tempSourceDir, true); } catch {}
        }
    }

    [Fact]
    public void TestLoadDtpNoFakeFallback()
    {
        string tempDtp = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.dtp");
        try
        {
            File.WriteAllText(tempDtp, "MFDTPF2 dummy data");
            
            var session = DtpTraceAnalyzer.LoadTrace(tempDtp);
            
            // Without a real JetBrains profiler dll and a real .dtp file, it must return null (no fake fallbacks)
            Assert.Null(session);
        }
        finally
        {
            try { File.Delete(tempDtp); } catch {}
        }
    }

    [Fact]
    public void TestDmwLoadWithReflection()
    {
        // 1. Create a dummy .dmw zip file
        string tempZip = Path.Combine(Path.GetTempPath(), $"test_ref_{Guid.NewGuid()}.dmw");
        string tempSourceDir = Path.Combine(Path.GetTempPath(), $"src_ref_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempSourceDir);

        try
        {
            string jsonContent = @"{
                ""Version"": ""2.0"",
                ""ProductName"": ""dotMemory"",
                ""ProductVersion"": ""2026.1.0.0"",
                ""ProfilingSessions"": [
                    {
                        ""Id"": ""00000000-0000-0000-0000-000000000000"",
                        ""ProcessName"": ""Reflected Session"",
                        ""CommandLine"": """",
                        ""ProcessId"": 1234,
                        ""StreamingStoragePath"": ""storage"",
                        ""Snapshots"": []
                    }
                ],
                ""sessions"": [
                    {
                        ""name"": ""Reflected Session""
                    }
                ]
            }";
            File.WriteAllText(Path.Combine(tempSourceDir, "workspace.json"), jsonContent);
            File.WriteAllText(Path.Combine(tempSourceDir, "000.dms.0000"), "dummy");

            System.IO.Compression.ZipFile.CreateFromDirectory(tempSourceDir, tempZip);

            var method = typeof(DmwSnapshotAnalyzer).GetMethod("TryLoadWithJetBrainsReflection", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(method);

            var session = new AnalyzedMemorySession();
            var success = (bool)method.Invoke(null, new object[] { tempSourceDir, session });

            string? modelDll = null;
            var searchPaths = new[]
            {
                "/Users/wieslawsoltes/Applications/Rider.app/Contents/plugins/dotTrace.dotMemory/DotFiles",
                "/Applications/Rider.app/Contents/plugins/dotTrace.dotMemory/DotFiles"
            };
            foreach (var path in searchPaths)
            {
                if (File.Exists(Path.Combine(path, "JetBrains.dotMemory.Model.dll")))
                {
                    modelDll = Path.Combine(path, "JetBrains.dotMemory.Model.dll");
                    break;
                }
            }

            if (modelDll != null)
            {
                Assert.True(success, "TryLoadWithJetBrainsReflection should succeed when SDK is present");
                Assert.Equal("Reflected Session", session.Name);
            }
            else
            {
                Assert.False(success);
            }
        }
        finally
        {
            try { File.Delete(tempZip); } catch {}
            try { Directory.Delete(tempSourceDir, true); } catch {}
        }
    }
}
