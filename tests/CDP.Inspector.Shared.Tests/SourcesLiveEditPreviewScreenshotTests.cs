using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Views;

namespace Avalonia.Diagnostics.Cdp.Tests;

public sealed class SourcesLiveEditPreviewScreenshotTests
{
    [AvaloniaFact]
    public void CapturesCompactV8MutationPreviewInSourcesHeader()
    {
        var app = Application.Current ?? throw new InvalidOperationException("Avalonia application is unavailable.");
        if (!app.Styles.OfType<StyleInclude>().Any(style =>
                style.Source?.ToString() == "avares://CDP.Inspector.Shared/Styles.axaml"))
        {
            app.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
            {
                Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
            });
        }

        var main = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService(), loadState: false);
        var sources = main.Sources;
        sources.SelectedFileName = "App.tsx";
        sources.SelectedFileContent = "export function App() {\n  const count: number = 3;\n  return <Counter value={count} />;\n}\n";
        sources.IsDebuggerEnabled = true;
        sources.IsDebuggerPaused = true;
        sources.DebuggerStatusText = "Paused on breakpoint · node";
        sources.PauseReason = "other";
        sources.LiveEditPreview = "esbuild regeneration · source 4→4 lines · output 6→8 lines";
        sources.LiveEditStatus = "V8 dry-run accepted";
        sources.CallFrames.Add(new V8CallFrameModel
        {
            CallFrameId = "frame-1",
            FunctionName = "renderCounter",
            Url = "file:///workspace/src/App.tsx",
            ScriptId = "42",
            LineNumber = 2,
            ColumnNumber = 9
        });
        sources.WatchExpressions.Add(new V8WatchExpressionModel
        {
            Expression = "count",
            Value = "3"
        });
        sources.ScopeVariables.Add(new V8ScopeVariableModel
        {
            Name = "count",
            ScopeType = "local",
            Type = "number",
            Value = "3",
            Writable = true
        });
        sources.V8Breakpoints.Add(new V8BreakpointModel
        {
            Key = "file:///workspace/src/App.tsx:2:0",
            Url = "file:///workspace/src/App.tsx",
            BindingUrl = "file:///workspace/dist/app.js",
            LineNumber = 2,
            DisplayLineNumber = 2,
            IsResolved = true
        });
        sources.ExecutionContexts.Add(new V8ExecutionContextModel
        {
            Id = 1,
            UniqueId = "node:context:1",
            Name = "node",
            Type = "default",
            IsDefault = true
        });

        var window = new Window
        {
            Width = 1440,
            Height = 900,
            Content = new SourcesView { DataContext = main }
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The Avalonia headless renderer did not return a frame.");
            var outputRoot = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
            }
            Directory.CreateDirectory(outputRoot);
            var path = Path.GetFullPath(Path.Combine(outputRoot, "sources-v8-mutation-preview.png"));
            frame.Save(path);

            Assert.True(File.Exists(path));
            Assert.Equal(1440, frame.PixelSize.Width);
            Assert.Equal(900, frame.PixelSize.Height);
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            window.Close();
        }
    }
}
