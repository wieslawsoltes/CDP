using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.CodeCompletion;
using CdpInspectorApp.Services;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Views;

namespace Avalonia.Diagnostics.Cdp.Tests;

public sealed class SourcesJavaScriptLanguageServiceTests
{
    [AvaloniaFact]
    public async Task SourcesEditorProvidesTypeScriptDiagnosticsAndCompletions()
    {
        var main = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService(), loadState: false);
        main.Sources.SelectedFileName = "app.ts";
        main.Sources.SelectedFileContent = "const message: number = 'wrong';\nconsole.log(message);\n";

        var view = new SourcesView { DataContext = main };
        var window = new Window
        {
            Width = 1100,
            Height = 720,
            Content = view
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var colorizer = GetField<LspDiagnosticColorizer>(view, "_diagnosticColorizer");
            await WaitUntilAsync(
                () => colorizer.Diagnostics.Count > 0,
                TimeSpan.FromSeconds(30));

            view.TxtSourceContent.Text = "console.";
            view.TxtSourceContent.CaretOffset = view.TxtSourceContent.Text.Length;
            view.TxtSourceContent.Focus();
            Dispatcher.UIThread.RunJobs();

            var showCompletion = typeof(SourcesView).GetMethod(
                "ShowCompletionAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Sources completion entry point was not found.");
            var completionTask = (Task?)showCompletion.Invoke(view, [true])
                ?? throw new InvalidOperationException("Sources completion did not return a task.");
            await completionTask;

            var completionWindow = GetField<CompletionWindow?>(view, "_completionWindow");
            Assert.NotNull(completionWindow);
            Assert.Contains(
                completionWindow.CompletionList.CompletionData.OfType<LspCompletionData>(),
                item => item.Text == "log" && item.DescriptionText.Contains("TypeScript", StringComparison.Ordinal));

            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The Avalonia headless renderer did not return a frame.");
            var outputRoot = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
            }
            Directory.CreateDirectory(outputRoot);
            frame.Save(Path.Combine(outputRoot, "sources-typescript-completion.png"));
        }
        finally
        {
            window.Close();
        }
    }

    private static T GetField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        return (T)field.GetValue(target)!;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (predicate()) return;
            await Task.Delay(100);
        }

        Assert.Fail($"Condition was not reached within {timeout}.");
    }
}
