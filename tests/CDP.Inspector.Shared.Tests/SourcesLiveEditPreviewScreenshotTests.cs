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
    public void CapturesV8LiveEditCompileErrorInSourcesHeader()
    {
        var window = CreateJavaScriptLiveEditWindow(
            "Live edit validation failed: Unexpected token ';' at main.jsx:14:27",
            "direct V8 script edit · 18 lines · dry-run rejected",
            "const increment = () => setCount(value + );");

        Capture(window, "sources-v8-live-edit-compile-error.png");
    }

    [AvaloniaFact]
    public void CapturesSourceMappedV8LiveEditSuccessInSourcesHeader()
    {
        var window = CreateJavaScriptLiveEditWindow(
            "Source-mapped live edit applied",
            "main.jsx → app.js · source 18→18 lines · output 116→116 lines",
            "const increment = () => setCount(value + 2);");

        Capture(window, "sources-v8-live-edit-applied.png");
    }

    [AvaloniaFact]
    public void CapturesExternalV8MutationPreviewInSourcesHeader()
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
        sources.SelectedFileName = "Counter.vue";
        sources.SelectedFileContent = "<script setup lang=\"ts\">\nconst count: number = 3;\n</script>\n<template><Counter :value=\"count\" /></template>\n";
        sources.IsDebuggerEnabled = true;
        sources.IsDebuggerPaused = true;
        sources.DebuggerStatusText = "Paused on breakpoint · WebScene V8";
        sources.PauseReason = "other";
        sources.LiveEditPreview = "Vue workspace compiler regeneration · source 4→4 lines · output 18→20 lines";
        sources.LiveEditStatus = "V8 dry-run accepted · source map and breakpoints ready";
        sources.CallFrames.Add(new V8CallFrameModel
        {
            CallFrameId = "frame-1",
            FunctionName = "renderCounter",
            Url = "file:///workspace/src/Counter.vue",
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
            Key = "file:///workspace/src/Counter.vue:2:0",
            Url = "file:///workspace/src/Counter.vue",
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

    [AvaloniaFact]
    public void CapturesWebAssemblyDisassemblyDebuggingWorkspace()
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
        sources.SelectedFileName = "math.wasm";
        sources.SelectedFileContent = """
            0x00000020  func $add
            0x00000022    local.get 0
            0x00000024    local.get 1
            0x00000026    i32.add
            0x00000027    end
            """;
        sources.IsDebuggerEnabled = true;
        sources.IsDebuggerPaused = true;
        sources.DebuggerStatusText = "Paused in WebAssembly · node";
        sources.PauseReason = "other";
        sources.LiveEditStatus = "WebAssembly disassembly · 5 lines · read-only";
        sources.RuntimeScripts.Add(new V8ScriptModel
        {
            ScriptId = "wasm-1",
            Url = "file:///workspace/dist/math.wasm",
            ScriptLanguage = "WebAssembly",
            BuildId = "wasm-build-1",
            Length = 46,
            DebugSymbols = new[] { new V8DebugSymbolModel { Type = "EmbeddedDWARF" } }
        });
        sources.CallFrames.Add(new V8CallFrameModel
        {
            CallFrameId = "wasm-frame-1",
            FunctionName = "$add",
            Url = "file:///workspace/dist/math.wasm",
            ScriptId = "wasm-1",
            LineNumber = 0,
            ColumnNumber = 38,
            ScopeChain = new[]
            {
                new V8ScopeModel { Type = "wasm-expression-stack", ObjectId = "wasm-scope-1" }
            }
        });
        sources.ScopeVariables.Add(new V8ScopeVariableModel
        {
            Name = "Expression stack",
            ScopeType = "wasm-expression-stack",
            Type = "scope",
            Value = "2 values",
            IsScopeGroup = true
        });
        sources.V8Breakpoints.Add(new V8BreakpointModel
        {
            Key = "wasm:wasm-build-1:36",
            ScriptId = "wasm-1",
            Url = "file:///workspace/dist/math.wasm",
            IsWebAssembly = true,
            BuildId = "wasm-build-1",
            LineNumber = 0,
            ColumnNumber = 36,
            DisplayLineNumber = 2,
            IsResolved = true
        });
        sources.ActiveDebugLine = 4;

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
            var path = Path.GetFullPath(Path.Combine(outputRoot, "sources-v8-wasm-debugging.png"));
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

    private static Window CreateJavaScriptLiveEditWindow(string status, string preview, string editedLine)
    {
        EnsureInspectorStyles();

        var main = new MainWindowViewModel(new MemoryViewModelTests.MockCdpService(), loadState: false);
        var sources = main.Sources;
        sources.SelectedFileName = "main.jsx";
        sources.SelectedFileContent = $$"""
            import React, { useState } from 'react';

            export function Counter() {
              const [value, setCount] = useState(1);
              {{editedLine}}

              return <button onClick={increment}>Count: {value}</button>;
            }
            """;
        sources.IsDebuggerEnabled = true;
        sources.IsDebuggerPaused = true;
        sources.DebuggerStatusText = "Paused on breakpoint · WebScene V8";
        sources.PauseReason = "other";
        sources.LiveEditPreview = preview;
        sources.LiveEditStatus = status;
        sources.ActiveDebugLine = 5;
        sources.CallFrames.Add(new V8CallFrameModel
        {
            CallFrameId = "frame-main",
            FunctionName = "increment",
            Url = "file:///workspace/src/main.jsx",
            ScriptId = "17",
            LineNumber = 4,
            ColumnNumber = 26
        });
        sources.WatchExpressions.Add(new V8WatchExpressionModel
        {
            Expression = "value",
            Value = "1"
        });
        sources.ScopeVariables.Add(new V8ScopeVariableModel
        {
            Name = "value",
            ScopeType = "closure",
            Type = "number",
            Value = "1",
            Writable = true
        });
        sources.V8Breakpoints.Add(new V8BreakpointModel
        {
            Key = "file:///workspace/src/main.jsx:5:0",
            Url = "file:///workspace/src/main.jsx",
            BindingUrl = "http://127.0.0.1:5173/assets/app.js",
            LineNumber = 4,
            DisplayLineNumber = 5,
            IsResolved = true
        });

        return new Window
        {
            Width = 1440,
            Height = 900,
            Content = new SourcesView { DataContext = main }
        };
    }

    private static void EnsureInspectorStyles()
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
    }

    private static void Capture(Window window, string fileName)
    {
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(350);
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The Avalonia headless renderer did not return a frame.");
            var outputRoot = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
            }

            Directory.CreateDirectory(outputRoot);
            var path = Path.GetFullPath(Path.Combine(outputRoot, fileName));
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
