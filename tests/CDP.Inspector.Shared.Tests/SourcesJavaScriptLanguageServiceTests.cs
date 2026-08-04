using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
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
        view.TxtSourceContent.Foreground = Brushes.White;
        view.TxtSourceContent.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
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

    [AvaloniaFact]
    public async Task SourcesEditorProvidesTypeScriptNavigationFormattingAndRename()
    {
        var main = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService(), loadState: false);
        main.Sources.SelectedFileName = "navigation.ts";
        main.Sources.SelectedFileContent =
            "const answer=41;\nfunction increment(value:number){return value+1;}\nconsole.log(increment(answer));\n";

        var view = new SourcesView { DataContext = main };
        view.TxtSourceContent.Foreground = Brushes.White;
        view.TxtSourceContent.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = view
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            await InvokeEditorActionAsync(view, "ShowJavaScriptSymbolsAsync");
            var symbols = view.TreeOutline.ItemsSource?.Cast<JavaScriptOutlineItem>().ToArray()
                ?? [];
            Assert.Contains(symbols, item => item.DisplayName.Contains("increment", StringComparison.Ordinal));

            view.TxtSourceContent.CaretOffset = view.TxtSourceContent.Text.LastIndexOf("answer", StringComparison.Ordinal);
            await InvokeEditorActionAsync(view, "ShowJavaScriptReferencesAsync");
            var references = view.TreeOutline.ItemsSource?.Cast<JavaScriptOutlineItem>().ToArray()
                ?? [];
            Assert.True(references.Length >= 2);

            await InvokeEditorActionAsync(view, "PrepareJavaScriptRenameAsync");
            var renameTextBox = view.FindControl<TextBox>("txtRenameSource")
                ?? throw new InvalidOperationException("The inline rename editor was not found.");
            renameTextBox.Text = "result";
            await InvokeEditorActionAsync(view, "ApplyPreparedJavaScriptRenameAsync");
            Assert.Contains("const result", view.TxtSourceContent.Text, StringComparison.Ordinal);
            Assert.Contains("increment(result)", view.TxtSourceContent.Text, StringComparison.Ordinal);

            var beforeFormat = view.TxtSourceContent.Text;
            await InvokeEditorActionAsync(view, "FormatJavaScriptDocumentAsync");
            Assert.NotEqual(beforeFormat, view.TxtSourceContent.Text);
            Assert.Contains("value: number", view.TxtSourceContent.Text, StringComparison.Ordinal);

            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The Avalonia headless renderer did not return a frame.");
            var outputRoot = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
            }
            Directory.CreateDirectory(outputRoot);
            frame.Save(Path.Combine(outputRoot, "sources-typescript-navigation-rename.png"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SourcesEditorProvidesKeyboardQuickOpenCommandsAndSymbolNavigation()
    {
        var main = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService(), loadState: false);
        main.Sources.SelectedFileName = "palette.ts";
        main.Sources.SelectedFileContent =
            "const answer = 41;\nfunction increment(value: number) { return value + 1; }\nconsole.log(increment(answer));\n";
        main.Sources.RuntimeScripts.Add(new CdpInspectorApp.Models.V8ScriptModel
        {
            ScriptId = "1",
            Url = "file:///app/counter.tsx",
            ScriptLanguage = "JavaScript",
            SourceContent = "export function Counter() { return 0; }"
        });
        main.Sources.RuntimeScripts.Add(new CdpInspectorApp.Models.V8ScriptModel
        {
            ScriptId = "2",
            Url = "file:///app/main.js",
            ScriptLanguage = "JavaScript",
            SourceContent = "console.log('main');"
        });

        var view = new SourcesView { DataContext = main };
        view.Foreground = Brushes.White;
        view.TxtSourceContent.Foreground = Brushes.White;
        view.TxtSourceContent.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        view.SourcePalette.Background = new SolidColorBrush(Color.Parse("#252526"));
        view.SourcePaletteQuery.Background = new SolidColorBrush(Color.Parse("#3C3C3C"));
        view.SourcePaletteQuery.Foreground = Brushes.White;
        view.SourcePaletteResults.Foreground = Brushes.White;
        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = view
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            view.TxtSourceContent.Focus();

            RaiseKeyDown(view.TxtSourceContent, Key.P, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.SourcePalette.IsVisible);
            Assert.Equal("Go to File", view.FindControl<TextBlock>("txtSourcePaletteTitle")?.Text);

            window.KeyTextInput("counter");
            Dispatcher.UIThread.RunJobs();
            var files = view.SourcePaletteResults.ItemsSource?.Cast<SourcesPaletteItem>().ToArray() ?? [];
            Assert.Single(files);
            Assert.Equal("counter.tsx", files[0].Label);

            RaiseKeyDown(view.SourcePaletteQuery, Key.Escape, KeyModifiers.None);
            RaiseKeyDown(view.TxtSourceContent, Key.P, KeyModifiers.Control | KeyModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Command Palette", view.FindControl<TextBlock>("txtSourcePaletteTitle")?.Text);
            window.KeyTextInput("step");
            Dispatcher.UIThread.RunJobs();
            var commands = view.SourcePaletteResults.ItemsSource?.Cast<SourcesPaletteItem>().ToArray() ?? [];
            Assert.Contains(commands, item => item.Label == "Step Over");
            Assert.Contains(commands, item => item.Label == "Step Into");

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The Avalonia headless renderer did not return a frame.");
            var outputRoot = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
            }
            Directory.CreateDirectory(outputRoot);
            frame.Save(Path.Combine(outputRoot, "sources-command-palette.png"));

            RaiseKeyDown(view.SourcePaletteQuery, Key.Escape, KeyModifiers.None);
            view.TxtSourceContent.CaretOffset = view.TxtSourceContent.Text.Length;
            view.TxtSourceContent.Focus();
            RaiseKeyDown(view.TxtSourceContent, Key.O, KeyModifiers.Control | KeyModifiers.Shift);
            await WaitUntilAsync(
                () => view.SourcePaletteResults.ItemsSource?.Cast<SourcesPaletteItem>()
                    .Any(item => item.Label == "increment") == true,
                TimeSpan.FromSeconds(30));
            window.KeyTextInput("increment");
            Dispatcher.UIThread.RunJobs();
            RaiseKeyDown(view.SourcePaletteQuery, Key.Enter, KeyModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(view.SourcePalette.IsVisible);
            Assert.Equal(view.TxtSourceContent.Text.IndexOf("increment", StringComparison.Ordinal),
                view.TxtSourceContent.CaretOffset);

            RaiseKeyDown(view.TxtSourceContent, Key.T, KeyModifiers.Control);
            await WaitUntilAsync(
                () => view.FindControl<TextBlock>("txtSourcePaletteTitle")?.Text == "Go to Symbol in Workspace" &&
                      view.SourcePaletteResults.ItemsSource?.Cast<SourcesPaletteItem>()
                          .Any(item => item.Label == "increment") == true,
                TimeSpan.FromSeconds(30));
            RaiseKeyDown(view.SourcePaletteQuery, Key.Escape, KeyModifiers.None);
            Assert.False(view.SourcePalette.IsVisible);
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

    private static void RaiseKeyDown(Control control, Key key, KeyModifiers modifiers)
    {
        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = Avalonia.Interactivity.RoutingStrategies.Tunnel,
            Source = control,
            Key = key,
            KeyModifiers = modifiers
        });
    }

    private static async Task InvokeEditorActionAsync(SourcesView view, string methodName)
    {
        var method = typeof(SourcesView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Sources editor action '{methodName}' was not found.");
        var task = (Task?)method.Invoke(view, null)
            ?? throw new InvalidOperationException($"Sources editor action '{methodName}' did not return a task.");
        await task;
        Dispatcher.UIThread.RunJobs();
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
