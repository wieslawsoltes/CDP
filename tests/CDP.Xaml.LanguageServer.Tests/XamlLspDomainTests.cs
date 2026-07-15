using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Avalonia.Diagnostics.Cdp.Domains;
using Xaml.Compiler.Parser;

namespace Avalonia.Diagnostics.Cdp.Tests
{
    public class XamlLspDomainTests
    {
        private string FindRepoRoot()
        {
            string? current = Directory.GetCurrentDirectory();
            while (current != null)
            {
                if (Directory.EnumerateFiles(current, "*.sln").Any() ||
                    Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }
                var parent = Directory.GetParent(current);
                if (parent == null || parent.FullName == current) break;
                current = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        [Fact]
        public async Task TestCompletions_FileNotFound_ReturnsEmpty()
        {
            var @params = new JsonObject
            {
                ["file"] = "NonExistentFile.axaml",
                ["line"] = 1,
                ["column"] = 1
            };

            var result = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
            Assert.NotNull(result);
            var completions = result["completions"] as JsonArray;
            Assert.NotNull(completions);
            Assert.Empty(completions);
        }

        [Fact]
        public async Task TestHover_FileNotFound_ReturnsEmpty()
        {
            var @params = new JsonObject
            {
                ["file"] = "NonExistentFile.axaml",
                ["line"] = 1,
                ["column"] = 1
            };

            var result = await XamlLspDomain.HandleAsync(null!, "getHover", @params);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task TestCompletions_EmptyFile_DoesNotCrash()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempEmptyFile.axaml");
            await File.WriteAllTextAsync(tempFile, "");

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 1,
                    ["column"] = 1
                };

                var result = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
                Assert.NotNull(result);
                var completions = result["completions"] as JsonArray;
                Assert.NotNull(completions);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestHover_EmptyFile_DoesNotCrash()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempEmptyFile.axaml");
            await File.WriteAllTextAsync(tempFile, "");

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 1,
                    ["column"] = 1
                };

                var result = await XamlLspDomain.HandleAsync(null!, "getHover", @params);
                Assert.NotNull(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestCompletions_OutOfBounds_DoesNotCrash()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempOutOfBounds.axaml");
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Button Content=""Click"" />
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                // Test extremely large bounds
                var @params1 = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 1000,
                    ["column"] = 1000
                };
                var result1 = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params1);
                Assert.NotNull(result1);

                // Test negative bounds
                var @params2 = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = -5,
                    ["column"] = -10
                };
                var result2 = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params2);
                Assert.NotNull(result2);

                // Test zero bounds
                var @params3 = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 0,
                    ["column"] = 0
                };
                var result3 = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params3);
                Assert.NotNull(result3);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestHover_OutOfBounds_DoesNotCrash()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempOutOfBounds.axaml");
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Button Content=""Click"" />
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                // Test extremely large bounds
                var @params1 = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 1000,
                    ["column"] = 1000
                };
                var result1 = await XamlLspDomain.HandleAsync(null!, "getHover", @params1);
                Assert.NotNull(result1);

                // Test negative bounds
                var @params2 = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = -5,
                    ["column"] = -10
                };
                var result2 = await XamlLspDomain.HandleAsync(null!, "getHover", @params2);
                Assert.NotNull(result2);

                // Test zero bounds
                var @params3 = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 0,
                    ["column"] = 0
                };
                var result3 = await XamlLspDomain.HandleAsync(null!, "getHover", @params3);
                Assert.NotNull(result3);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestCompletions_MalformedXaml_DoesNotCrash()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempMalformed.axaml");
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui"">
    <StackPanel>
        <Button Content=
    </StackPanel>
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 3,
                    ["column"] = 24
                };
                var result = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
                Assert.NotNull(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestHover_MalformedXaml_DoesNotCrash()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempMalformed.axaml");
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui"">
    <StackPanel>
        <Button Content=
    </StackPanel>
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 3,
                    ["column"] = 10
                };
                var result = await XamlLspDomain.HandleAsync(null!, "getHover", @params);
                Assert.NotNull(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestCompletions_MissingParams_DoesNotCrash()
        {
            var @params = new JsonObject();
            var exception = await Record.ExceptionAsync(async () =>
            {
                await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
            });
            Assert.Null(exception);
        }

        [Fact]
        public async Task TestCompletions_InvalidParamTypes_ThrowsException()
        {
            var @params = new JsonObject
            {
                ["file"] = "NonExistent.axaml",
                ["line"] = "not_an_int",
                ["column"] = new JsonArray(1, 2, 3)
            };

            var exception = await Record.ExceptionAsync(async () =>
            {
                await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
            });

            Assert.NotNull(exception);
            Assert.IsAssignableFrom<Exception>(exception);
        }

        [Fact]
        public async Task TestHover_UnknownElement_ThrowsTypeLoadException()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempUnknownElement.axaml");
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <NonExistentControl />
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 3,
                    ["column"] = 10
                };
                
                var result = await XamlLspDomain.HandleAsync(null!, "getHover", @params);
                Assert.NotNull(result);
                Assert.Empty(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestCompletions_UnknownElement_ThrowsTypeLoadException()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempUnknownElement.axaml");
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <NonExistentControl UnknownProperty=""Value"" />
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 3,
                    ["column"] = 30
                };
                
                var result = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
                Assert.NotNull(result);
                var completions = result["completions"] as JsonArray;
                Assert.NotNull(completions);
                Assert.Empty(completions);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestCompletions_EnclosingElementBug_ForWindowAttribute()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempWindowAttributeBug.axaml");
            
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui""
        Ti>
    <StackPanel>
        <Button Content=""Click"" />
    </StackPanel>
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 2,
                    ["column"] = 11
                };
                
                var result = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
                Assert.NotNull(result);
                var completions = result["completions"] as JsonArray;
                Assert.NotNull(completions);
                
                Assert.NotEmpty(completions);
                Assert.Contains(completions, c => c?["label"]?.GetValue<string>() == "Title");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task TestCompletions_CustomClrNamespace_ResolvesAndReturnsCompletions()
        {
            string repoRoot = FindRepoRoot();
            string tempFile = Path.Combine(repoRoot, "TempClrNamespace.axaml");
            
            string xaml = @"<Window xmlns=""https://github.com/avaloniaui""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        xmlns:hl=""clr-namespace:Avalonia.Diagnostics.Cdp;assembly=Avalonia.Diagnostics.Cdp"">
    <hl:HighlightAdorner >
    </hl:HighlightAdorner>
</Window>";
            await File.WriteAllTextAsync(tempFile, xaml);

            try
            {
                var @params = new JsonObject
                {
                    ["file"] = tempFile,
                    ["line"] = 4,
                    ["column"] = 26
                };
                
                var result = await XamlLspDomain.HandleAsync(null!, "getCompletions", @params);
                Assert.NotNull(result);
                var completions = result["completions"] as JsonArray;
                Assert.NotNull(completions);
                
                var labels = completions.Select(c => c?["label"]?.GetValue<string>() ?? "").ToList();
                Assert.Contains("Width", labels);
                Assert.Contains("ClipToBounds", labels);
                Assert.Contains("AdornedVisual", labels);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}




