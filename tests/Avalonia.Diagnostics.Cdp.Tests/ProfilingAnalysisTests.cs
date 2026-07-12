using System;
using System.IO;
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
    public void TestLoadDtpWithFallback()
    {
        string tempDtp = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.dtp");
        try
        {
            File.WriteAllText(tempDtp, "MFDTPF2 dummy data");
            
            var session = DtpTraceAnalyzer.LoadTrace(tempDtp);
            
            Assert.NotNull(session);
            Assert.Equal(Path.GetFileNameWithoutExtension(tempDtp), session.Name);
            Assert.True(session.TotalDurationMs > 0);
            Assert.NotEmpty(session.Blocks);
            Assert.NotEmpty(session.MethodStats);
            Assert.NotEmpty(session.CallTreeRoots);

            // Verify call tree hierarchy structure
            var root = session.CallTreeRoots[0];
            Assert.Equal("(root)", root.Name);
            Assert.NotEmpty(root.Children);

            var idle = root.Children.FirstOrDefault(c => c.Name == "(idle)");
            Assert.NotNull(idle);
        }
        finally
        {
            try { File.Delete(tempDtp); } catch {}
        }
    }
}
