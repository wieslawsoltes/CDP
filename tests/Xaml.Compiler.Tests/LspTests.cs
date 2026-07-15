using System.IO;
using System.Linq;
using Xunit;
using CDP.Xaml.LanguageServer;
using CDP.CSharp.LanguageServer;
using Xaml.Compiler.Registry;

namespace Xaml.Compiler.Tests
{
    public class LspTestControl
    {
        public string? Title { get; set; }
    }

    public class LspTests
    {
        [Fact]
        public void TestXamlLanguageServer_CompletionsAndHover()
        {
            var registry = new XamlSchemaRegistry();
            var mockNs = new Xaml.Compiler.TypeSystemImpl.XamlNamespace(
                "mock-uri",
                "m",
                new[] { typeof(LspTests).Assembly.GetName().Name! },
                new[] { typeof(LspTests).Namespace! }
            );
            registry.RegisterNamespace(mockNs);

            var server = new XamlLanguageServer(registry);
            string uri = "file:///temp.axaml";
            string text = "<m:LspTestControl xmlns:m=\"mock-uri\" Title=\"Hello\" />";
            
            server.OpenDocument(uri, text);

            // Col 42 is inside "Title" attribute key
            var completions = server.GetCompletions(uri, 1, 42);
            Assert.NotEmpty(completions);
            Assert.Contains(completions, c => c.Label == "Title");

            // Col 10 is inside "LspTestControl" tag
            var hover = server.GetHover(uri, 1, 10);
            Assert.NotNull(hover);
            Assert.Contains("LspTestControl", hover.Contents);
        }

        [Fact]
        public void TestCSharpLanguageServer_CompletionsDiagnosticsAndHover()
        {
            var server = new CSharpLanguageServer();
            string uri = "file:///temp.cs";
            string text = "using System; class MyClass { void Run() { Console. } }";

            server.OpenDocument(uri, text);

            // Col 52 is right after "Console."
            var completions = server.GetCompletions(uri, 1, 52);
            Assert.NotEmpty(completions);
            Assert.Contains(completions, c => c.Label == "WriteLine");

            // Col 20 is inside "MyClass"
            var hover = server.GetHover(uri, 1, 20);
            Assert.NotNull(hover);
            Assert.Contains("MyClass", hover.Contents);

            var diagnostics = server.GetDiagnostics(uri);
            // "Console." is syntax invalid because it's incomplete
            Assert.NotEmpty(diagnostics);

            string validText = "class MyClass { void Run() { } }";
            server.ChangeDocument(uri, validText);
            var validDiags = server.GetDiagnostics(uri);
            Assert.Empty(validDiags);
        }
    }
}
