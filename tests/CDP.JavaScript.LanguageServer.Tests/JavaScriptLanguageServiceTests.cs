using CDP.JavaScript.LanguageServer;

namespace CDP.JavaScript.LanguageServer.Tests;

public sealed class JavaScriptLanguageServiceTests
{
    [Fact]
    public async Task ProvidesRealTypeScriptSemanticsAcrossProjectDocuments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new JavaScriptLanguageService();
        await service.OpenProjectAsync(
        [
            new("/workspace/math.ts", "export function add(left: number, right: number) { return left + right; }"),
            new("/workspace/app.ts", "import { add } from './math';\nconst total = add(20, 22);\nconsole.log(total);\n")
        ], "/workspace", cancellationToken);

        var completions = await service.GetCompletionsAsync("/workspace/app.ts", 3, 9, cancellationToken);
        var hover = await service.GetQuickInfoAsync("/workspace/app.ts", 2, 16, cancellationToken);
        var definitions = await service.GetDefinitionsAsync("/workspace/app.ts", 2, 16, cancellationToken);
        var references = await service.GetReferencesAsync("/workspace/app.ts", 2, 16, cancellationToken);
        var symbols = await service.GetDocumentSymbolsAsync("/workspace/math.ts", cancellationToken);
        var semantic = await service.GetSemanticClassificationsAsync("/workspace/app.ts", cancellationToken);

        Assert.StartsWith("5.9", service.TypeScriptVersion);
        Assert.Contains(completions, item => item.Name == "log");
        Assert.NotNull(hover);
        Assert.Contains("add", hover.DisplayText);
        Assert.Contains(definitions, item => item.FileName.EndsWith("/math.ts", StringComparison.Ordinal));
        Assert.True(references.Count >= 2);
        Assert.NotNull(symbols);
        Assert.Contains(symbols.Children, item => item.Text == "add" && item.Kind == "function");
        Assert.NotEmpty(semantic);
    }

    [Fact]
    public async Task ReportsDiagnosticsSignatureRenameAndFormatting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new JavaScriptLanguageService();
        const string source = "function greet(name: string, count: number) { return name.repeat(count); }\n"
            + "const message: number = greet('Ada', 2);\n"
            + "greet('Grace', );\n";
        await service.OpenDocumentAsync("/workspace/app.ts", source, "/workspace", cancellationToken);

        var diagnostics = await service.GetDiagnosticsAsync("/workspace/app.ts", cancellationToken);
        var signature = await service.GetSignatureHelpAsync("/workspace/app.ts", 3, 16, cancellationToken);
        var rename = await service.GetRenameLocationsAsync("/workspace/app.ts", 1, 11, cancellationToken);
        var formatting = await service.GetFormattingEditsAsync(
            "/workspace/app.ts",
            cancellationToken: cancellationToken);

        Assert.Contains(diagnostics, item => item.Code == 2322);
        Assert.NotNull(signature);
        Assert.Equal(1, signature.ArgumentIndex);
        Assert.Contains(signature.Items, item =>
            item.Parameters.Count == 2 && item.Parameters[0].Name == "name");
        Assert.True(rename.CanRename);
        Assert.True(rename.Locations.Count >= 3);
        Assert.NotEmpty(formatting);

        var formatted = JavaScriptLanguageService.ApplyTextChanges(source, formatting);
        Assert.EndsWith("\n", formatted, StringComparison.Ordinal);
        Assert.Contains("function greet", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnderstandsJavaScriptAndJsxDocuments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new JavaScriptLanguageService();
        await service.OpenProjectAsync(
        [
            new("/workspace/jsx.d.ts", "declare namespace JSX { interface IntrinsicElements { button: any; } } declare module 'react/jsx-runtime' { export const jsx: any; export const jsxs: any; export const Fragment: any; }"),
            new("/workspace/component.jsx", "/** @param {{ count: number }} props */\nexport const Counter = (props) => <button>{props.count.toFixed(0)}</button>;"),
            new("/workspace/main.js", "import { Counter } from './component.jsx';\nCounter({ count: 'wrong' });")
        ], "/workspace", cancellationToken);

        var hover = await service.GetQuickInfoAsync("/workspace/component.jsx", 2, 14, cancellationToken);
        var definitions = await service.GetDefinitionsAsync("/workspace/main.js", 2, 2, cancellationToken);
        var diagnostics = await service.GetDiagnosticsAsync("/workspace/component.jsx", cancellationToken);

        Assert.NotNull(hover);
        Assert.Contains("Counter", hover.DisplayText);
        Assert.Contains(definitions, item => item.FileName.EndsWith("/component.jsx", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, item => item.Message.Contains("IntrinsicElements", StringComparison.Ordinal));
    }
}
