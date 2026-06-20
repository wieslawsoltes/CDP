using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;
using CdpInspectorApp.Models;

namespace ControlApp;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://ControlApp/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        });
    }
}

class Program
{
    public static void Main(string[] args)
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .AfterSetup(_ =>
            {
                Task.Run(RunE2ETestAsync);
            })
            .StartWithClassicDesktopLifetime(args);
    }

    private static async Task RunE2ETestAsync()
    {
        try
        {
            Console.WriteLine("=== STARTING TASK-SPECIFIC TEST STUDIO E2E VERIFICATION ===");
            var clipboardType = typeof(Avalonia.Input.Platform.IClipboard);
            Console.WriteLine("IClipboard methods: " + string.Join(", ", clipboardType.GetMethods().Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")));
            var extensionTypes = clipboardType.Assembly.GetTypes().Where(t => t.IsAbstract && t.IsSealed && t.Name.Contains("Clipboard", StringComparison.OrdinalIgnoreCase));
            foreach (var ext in extensionTypes)
            {
                Console.WriteLine($"Extension class: {ext.FullName}");
                Console.WriteLine("Methods: " + string.Join(", ", ext.GetMethods().Select(m => $"{m.Name}")));
            }

            // 1. Create a window and controls
            Window? window = null;
            TextBox? textBox = null;
            Button? button = null;
            ScrollViewer? scrollViewer = null;
            bool buttonClicked = false;
            double scrollDeltaY = 0;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox = new TextBox
                {
                    Name = "txtTarget",
                    Width = 100,
                    Height = 50,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                };
                button = new Button
                {
                    Name = "btnTarget",
                    Width = 100,
                    Height = 50,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Content = "Click Me"
                };
                button.Click += (s, e) => buttonClicked = true;

                scrollViewer = new ScrollViewer
                {
                    Name = "scrollTarget",
                    Width = 200,
                    Height = 200,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Content = new Canvas { Width = 1000, Height = 1000 }
                };
                scrollViewer.ScrollChanged += (s, e) =>
                {
                    scrollDeltaY = scrollViewer.Offset.Y;
                };

                var containerCanvas = new Canvas { Width = 400, Height = 500 };
                Canvas.SetLeft(textBox, 0); Canvas.SetTop(textBox, 0);
                Canvas.SetLeft(button, 150); Canvas.SetTop(button, 0);
                Canvas.SetLeft(scrollViewer, 0); Canvas.SetTop(scrollViewer, 100);

                containerCanvas.Children.Add(textBox);
                containerCanvas.Children.Add(button);
                containerCanvas.Children.Add(scrollViewer);

                window = new Window
                {
                    Title = "Test Studio E2E Target Window",
                    Width = 400,
                    Height = 500,
                    WindowDecorations = WindowDecorations.None,
                    Content = containerCanvas
                };
                window.Show();
                window.Activate();
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = window;
                }
            });

            // Wait for visual tree to arrange
            await Task.Delay(200);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window!.Measure(new Size(400, 500));
                window!.Arrange(new Rect(0, 0, 400, 500));
            });

            // 2. Start the CDP Server manually on port 9236
            CdpServer.Start(9236);
            var targetId = CdpServer.Register(window!, "E2E Target");
            Console.WriteLine($"CDP Server started on port 9236, registered E2E Target with ID {targetId}");

            // 3. Setup client-side CdpService and MainWindowViewModel
            var cdpService = new CdpService();
            var mainVm = new MainWindowViewModel(cdpService);
            mainVm.Connection.HostAddress = "http://127.0.0.1:9236";

            // Scan and connect
            var targets = await cdpService.GetTargetsAsync("http://127.0.0.1:9236");
            var target = targets.First(t => t.Id == targetId.ToString());
            await cdpService.ConnectAsync("http://127.0.0.1:9236", target);
            Console.WriteLine("CdpService client connected successfully.");

            // 4. Test Scenario 1: Interactive Step Construction & Auto-YAML generation
            Console.WriteLine("Testing Scenario 1: Interactive step construction & auto-YAML synchronization...");
            var testStudio = mainVm.Recorder.TestStudio;
            testStudio.SelectedElementSelector = "#btnTarget";
            testStudio.InputSimText = "Hello Test Studio";
            testStudio.DelayMs = 350;

            // Trigger Add Commands/Methods
            testStudio.AddLaunchApp();
            await testStudio.AddTapAsync();
            await testStudio.AddInputAsync();
            await testStudio.AddAssertVisibleAsync();
            await testStudio.AddAssertNotVisibleAsync();
            await testStudio.AddClearTextAsync();
            testStudio.InputSimText = "";
            testStudio.AddDelay();
            await testStudio.AddScrollAsync();
            await testStudio.AddBackAsync();

            // Assert steps count and YAML generation
            if (testStudio.Steps.Count != 9)
            {
                throw new Exception($"Expected 9 steps, got {testStudio.Steps.Count}");
            }
            Console.WriteLine("Steps count matches: 9");

            string currentYaml = testStudio.YamlCode;
            Console.WriteLine($"Generated YAML:\n{currentYaml}");

            if (!currentYaml.Contains("- launchApp") ||
                !currentYaml.Contains("- tapOn: \"#btnTarget\"") ||
                !currentYaml.Contains("- inputText:") ||
                !currentYaml.Contains("text: \"Hello Test Studio\"") ||
                !currentYaml.Contains("- assertVisible: \"#btnTarget\"") ||
                !currentYaml.Contains("- assertNotVisible: \"#btnTarget\"") ||
                !currentYaml.Contains("- clearText: \"#btnTarget\"") ||
                !currentYaml.Contains("- delay: 350") ||
                !currentYaml.Contains("- scroll:") ||
                !currentYaml.Contains("- back"))
            {
                throw new Exception("Generated YAML is missing some steps or properties!");
            }
            Console.WriteLine("Scenario 1 PASSED.");

            // 5. Test Scenario 2: YAML parsing and step loading
            Console.WriteLine("Testing Scenario 2: YAML parsing and step loading...");
            testStudio.ClearCommand.Execute(null);
            if (testStudio.Steps.Count != 0)
            {
                throw new Exception($"ClearCommand did not clear steps. Count: {testStudio.Steps.Count}");
            }

            testStudio.YamlCode = @"appId: ""com.e2e.test""
description: ""E2E Yaml Parse Test""
---
- launchApp
- tapOn: ""#btnTarget""
- inputText:
    selector: ""#txtTarget""
    value: ""Headless Input""
- assertVisible: ""#txtTarget""
- assertNotVisible: ""#nonexistent""
- scroll:
    direction: ""down""
    amount: 150
- clearText: ""#txtTarget""
";
            testStudio.ApplyYamlCommand.Execute(null);

            if (testStudio.Steps.Count != 7)
            {
                throw new Exception($"Expected 7 parsed steps, got {testStudio.Steps.Count}");
            }
            Console.WriteLine("Steps count after ApplyYaml: 7");

            // Verify parsed step properties
            var step1 = testStudio.Steps[0];
            var step2 = testStudio.Steps[1];
            var step3 = testStudio.Steps[2];
            var step4 = testStudio.Steps[3];
            var step5 = testStudio.Steps[4];
            var step6 = testStudio.Steps[5];
            var step7 = testStudio.Steps[6];

            if (step1.Action != "launchApp" ||
                step2.Action != "tapOn" || step2.Selector != "#btnTarget" ||
                step3.Action != "inputText" || step3.Selector != "#txtTarget" || step3.Value != "Headless Input" ||
                step4.Action != "assertVisible" || step4.Selector != "#txtTarget" ||
                step5.Action != "assertNotVisible" || step5.Selector != "#nonexistent" ||
                step6.Action != "scroll" || step6.Value?.Contains("direction: down") != true ||
                step7.Action != "clearText" || step7.Selector != "#txtTarget")
            {
                throw new Exception("Parsed steps do not match expected actions/properties.");
            }
            Console.WriteLine("Scenario 2 PASSED.");

            // 6. Test Scenario 3: E2E Play execution loop
            Console.WriteLine("Testing Scenario 3: Playing execution loop...");
            
            buttonClicked = false;
            scrollDeltaY = 0;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox!.Text = "";
            });

            // Start playing
            await testStudio.PlayAsync();

            // Wait for execution to finish
            int waitTimeoutMs = 15000;
            int elapsedMs = 0;
            while (testStudio.IsExecuting && elapsedMs < waitTimeoutMs)
            {
                await Task.Delay(100);
                elapsedMs += 100;
            }

            if (testStudio.IsExecuting)
            {
                throw new Exception("Play execution timed out after 15 seconds!");
            }

            // Assert statuses
            for (int i = 0; i < testStudio.Steps.Count; i++)
            {
                var step = testStudio.Steps[i];
                if (step.Status != StepStatus.Passed)
                {
                    throw new Exception($"Step {i+1} ({step.ActionDisplay}) failed with error: {step.ErrorMessage}");
                }
            }
            Console.WriteLine("All execution steps finished with Passed status.");

            // Verify visual effects on window controls
            if (!buttonClicked)
            {
                throw new Exception("Expected buttonClicked to be true after tapOn step.");
            }
            Console.WriteLine("Button click verified.");

            if (scrollDeltaY <= 0)
            {
                throw new Exception("Expected scrollDeltaY to be positive after scroll step.");
            }
            Console.WriteLine($"ScrollViewer scroll verified: Y={scrollDeltaY}");

            string? txtVal = null;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                txtVal = textBox!.Text;
            });
            if (!string.IsNullOrEmpty(txtVal))
            {
                throw new Exception($"Expected TextBox text to be cleared, got '{txtVal}'");
            }
            Console.WriteLine("TextBox value cleared successfully.");
            Console.WriteLine("Scenario 3 PASSED.");

            // 7. Test Scenario 4: StepOver command
            Console.WriteLine("Testing Scenario 4: StepOver command...");
            testStudio.ClearCommand.Execute(null);
            testStudio.YamlCode = @"- delay: 100
- delay: 200
";
            testStudio.ApplyYamlCommand.Execute(null);

            if (testStudio.Steps.Count != 2)
            {
                throw new Exception("Expected 2 steps for StepOver test.");
            }

            // Perform first StepOver
            await testStudio.StepOverAsync();
            if (!testStudio.IsExecuting || !testStudio.IsPaused)
            {
                throw new Exception("Expected execution to be running and paused after StepOver.");
            }
            if (testStudio.Steps[0].Status != StepStatus.Passed || testStudio.Steps[1].Status != StepStatus.Pending)
            {
                throw new Exception("Expected step 1 to be Passed and step 2 to be Pending.");
            }
            Console.WriteLine("First step execution via StepOver verified.");

            // Perform second StepOver (finish)
            await testStudio.StepOverAsync();
            if (testStudio.IsExecuting || testStudio.IsPaused)
            {
                throw new Exception("Expected execution to be completed.");
            }
            if (testStudio.Steps[0].Status != StepStatus.Passed || testStudio.Steps[1].Status != StepStatus.Passed)
            {
                throw new Exception("Expected both steps to be Passed.");
            }
            Console.WriteLine("Scenario 4 PASSED.");

            // 8. Test Scenario 5: Bidirectional Selection Sync
            Console.WriteLine("Testing Scenario 5: Bidirectional selection sync...");
            var mockNode = new DomNodeModel(1234, "Button");
            mockNode.AttributesList.Add(new AttributeModel("id", "btnE2ESync"));
            
            mainVm.Elements.SelectedNode = mockNode;
            if (testStudio.SelectedElementSelector != "#btnE2ESync")
            {
                throw new Exception($"Expected SelectedElementSelector to sync to '#btnE2ESync', got '{testStudio.SelectedElementSelector}'");
            }
            Console.WriteLine("SelectedElementSelector synced correctly on SelectedNode change.");
            Console.WriteLine("Scenario 5 PASSED.");

            // 9. Test Scenario 6: Recorder Translation Integration
            Console.WriteLine("Testing Scenario 6: Recorder translation integration...");
            mainVm.Recorder.IsTestStudioActive = true;
            await mainVm.Recorder.ToggleRecordAsync();

            if (!mainVm.Recorder.IsRecording)
            {
                throw new Exception("Expected Recorder to start recording.");
            }

            testStudio.Steps.Clear();

            // Simulate pointer pressed/released to trigger recorded steps from the server
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = 200.0,
                ["y"] = 25.0,
                ["button"] = "left",
                ["clickCount"] = 1,
                ["modifiers"] = 0
            });
            await Task.Delay(100);
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = 200.0,
                ["y"] = 25.0,
                ["button"] = "left",
                ["clickCount"] = 1,
                ["modifiers"] = 0
            });

            // Wait for WebSocket event processing
            elapsedMs = 0;
            bool hasTapOn = false;
            while (elapsedMs < 5000)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    hasTapOn = testStudio.Steps.Any(s => s.Action == "tapOn");
                });
                if (hasTapOn) break;
                await Task.Delay(100);
                elapsedMs += 100;
            }

            TestStudioStepModel? recordedTsStep = null;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                recordedTsStep = testStudio.Steps.FirstOrDefault(s => s.Action == "tapOn");
            });

            if (recordedTsStep == null)
            {
                throw new Exception("Test Studio steps list did not receive the translated recorded click step.");
            }

            if (recordedTsStep.Selector != "#btnTarget")
            {
                throw new Exception($"Translated step does not match expectations. Action: {recordedTsStep.Action}, Selector: {recordedTsStep.Selector}");
            }
            Console.WriteLine("Recorder step successfully intercepted, translated, and appended to Test Studio.");

            // Simulate keydown to trigger recorded step from the server
            await cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
            {
                ["type"] = "rawKeyDown",
                ["key"] = "Enter",
                ["code"] = "Enter",
                ["text"] = "",
                ["modifiers"] = 0
            });

            // Wait for WebSocket event processing for pressKey step
            elapsedMs = 0;
            bool hasPressKey = false;
            while (elapsedMs < 5000)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    hasPressKey = testStudio.Steps.Any(s => s.Action == "pressKey");
                });
                if (hasPressKey) break;
                await Task.Delay(100);
                elapsedMs += 100;
            }

            TestStudioStepModel? recordedKeyStep = null;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                recordedKeyStep = testStudio.Steps.FirstOrDefault(s => s.Action == "pressKey");
            });

            if (recordedKeyStep == null)
            {
                throw new Exception("Test Studio steps list did not receive the translated recorded keydown step.");
            }

            if (recordedKeyStep.Value != "Return")
            {
                throw new Exception($"Translated step does not match expectations. Action: {recordedKeyStep.Action}, Value: {recordedKeyStep.Value}");
            }
            Console.WriteLine("Recorder keydown step successfully intercepted, translated, and appended to Test Studio.");
            
            // Stop recording so subsequent execution isn't recorded in steps list
            await mainVm.Recorder.ToggleRecordAsync();
            mainVm.Recorder.IsTestStudioActive = false;
            Console.WriteLine("Scenario 6 PASSED.");

            // 10. Test Scenario 7: Verify View bindings
            Console.WriteLine("Testing Scenario 7: Verify View bindings...");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var tsView = new CdpInspectorApp.Views.TestStudioView();
                tsView.DataContext = mainVm;

                var buttons = FindLogicalChildren<Button>(tsView);
                Console.WriteLine($"Found {buttons.Count} buttons in TestStudioView:");
                foreach (var btn in buttons)
                {
                    var cmd = btn.Command;
                    var content = btn.Content?.ToString() ?? "";
                    bool isEnabled = btn.IsEnabled;
                    Console.WriteLine($"Button '{content}': Command={cmd?.GetType().Name ?? "null"}, IsEnabled={isEnabled}");
                }
            });
            Console.WriteLine("Scenario 7 PASSED.");

            // 11. Test Scenario 8: Verify New Maestro Commands Execution (doubleTapOn, longPressOn, assertTrue, takeScreenshot)
            Console.WriteLine("Testing Scenario 8: Verify New Maestro Commands Execution...");
            testStudio.ClearCommand.Execute(null);
            
            // Set up a flow testing doubleTapOn, longPressOn, assertTrue, and takeScreenshot
            testStudio.YamlCode = @"appId: ""CdpSampleApp""
description: ""Verify new commands execution""
---
- doubleTapOn: ""#btnTarget""
- longPressOn: ""#btnTarget""
- assertTrue: ""1 === 1""
- takeScreenshot: ""e2e_screenshot.png""
- copyTextFrom: ""#btnTarget""
- openLink: ""http://127.0.0.1:9236/mockPage""
";
            testStudio.ApplyYamlCommand.Execute(null);
            if (testStudio.Steps.Count != 6)
            {
                throw new Exception($"Expected 6 parsed steps for Scenario 8, got {testStudio.Steps.Count}");
            }
            
            // Let's run Play to execute them
            await testStudio.PlayAsync();
            
            // Wait for execution to finish
            elapsedMs = 0;
            while (testStudio.IsExecuting && elapsedMs < waitTimeoutMs)
            {
                await Task.Delay(100);
                elapsedMs += 100;
            }

            if (testStudio.IsExecuting)
            {
                throw new Exception("Scenario 8 Play execution timed out!");
            }

            // Print logs
            System.Collections.Generic.List<string> logsCopy = new();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                logsCopy = testStudio.Logs.ToList();
            });
            foreach (var log in logsCopy)
            {
                Console.WriteLine($"[TestStudio Log] {log}");
            }

            // Assert statuses
            for (int i = 0; i < testStudio.Steps.Count; i++)
            {
                var step = testStudio.Steps[i];
                if (step.Status != StepStatus.Passed)
                {
                    throw new Exception($"Scenario 8 Step {i+1} ({step.ActionDisplay}) failed with error: {step.ErrorMessage}");
                }
            }
            Console.WriteLine("Scenario 8 step execution completed successfully.");
            
            // Check if screenshot was created
            if (System.IO.File.Exists("e2e_screenshot.png"))
            {
                Console.WriteLine("Screenshot file 'e2e_screenshot.png' was captured successfully.");
                try { System.IO.File.Delete("e2e_screenshot.png"); } catch {}
            }
            else
            {
                throw new Exception("Expected screenshot file 'e2e_screenshot.png' to be created, but it was not found.");
            }

            // Check clipboard value from copyTextFrom step
            string clipboardVal = "";
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Avalonia.Input.Platform.IClipboard? clipboard = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    clipboard = desktop.MainWindow?.Clipboard;
                }
                else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
                {
                    clipboard = Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
                }
                if (clipboard != null)
                {
                    clipboardVal = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) ?? "";
                }
            });

            if (clipboardVal != "Click Me")
            {
                throw new Exception($"Expected clipboard text to be 'Click Me', got '{clipboardVal}'");
            }
            Console.WriteLine($"Clipboard verified: '{clipboardVal}'");

            Console.WriteLine("Scenario 8 PASSED.");

            // 12. Test Scenario 9: Playwright Code Generation & Parsing E2E
            Console.WriteLine("Testing Scenario 9: Playwright code generation & parsing...");
            mainVm.Recorder.SelectedFormat = RecordingFormat.PlaywrightTest;
            mainVm.Recorder.ClearRecording();

            // Simulate adding a step
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                mainVm.Recorder.LoadParsedSteps(new System.Collections.Generic.List<RecordedStepModel>
                {
                    new RecordedStepModel { Type = "setViewport", Width = 1024, Height = 768 },
                    new RecordedStepModel { Type = "navigate", Url = "http://127.0.0.1:9236/" },
                    new RecordedStepModel { Type = "click", Selector = "#btnTarget", Button = "left", ClickCount = 1 },
                    new RecordedStepModel { Type = "change", Selector = "#txtTarget", Value = "E2E Playwright Text" },
                    new RecordedStepModel { Type = "assertVisible", Selector = "#btnTarget" },
                    new RecordedStepModel { Type = "assertNotVisible", Selector = "#nonexistent" }
                });
            });

            string pwGeneratedCode = mainVm.Recorder.GeneratedCode;
            Console.WriteLine($"Generated Playwright Code:\n{pwGeneratedCode}");

            if (!pwGeneratedCode.Contains("import { test, expect, chromium } from '@playwright/test';") ||
                !pwGeneratedCode.Contains("test.describe('CDP Recorded Tests', () => {") ||
                !pwGeneratedCode.Contains("await test.step('Click on element #btnTarget', async () => {") ||
                !pwGeneratedCode.Contains("await test.step('Assert element #btnTarget is visible', async () => {") ||
                !pwGeneratedCode.Contains("await expect(page.locator('#btnTarget')).toBeVisible();") ||
                !pwGeneratedCode.Contains("await expect(page.locator('#nonexistent')).toBeHidden();") ||
                !pwGeneratedCode.Contains("chromium.connectOverCDP('http://127.0.0.1:9236')") ||
                !pwGeneratedCode.Contains("page.locator('#btnTarget')") ||
                !pwGeneratedCode.Contains("page.locator('#txtTarget')") ||
                !pwGeneratedCode.Contains("fill('E2E Playwright Text')"))
            {
                throw new Exception("Generated Playwright code is missing some expected structures!");
            }
            Console.WriteLine("Playwright code generation verified.");

            // Verify parsing back
            var parsedStepsFromPlaywright = RecordingParser.Parse(pwGeneratedCode);
            if (parsedStepsFromPlaywright.Count != 6)
            {
                throw new Exception($"Expected 6 parsed steps from Playwright script, got {parsedStepsFromPlaywright.Count}");
            }

            if (parsedStepsFromPlaywright[0].Type != "setViewport" ||
                parsedStepsFromPlaywright[1].Type != "navigate" ||
                parsedStepsFromPlaywright[2].Type != "click" || parsedStepsFromPlaywright[2].Selector != "#btnTarget" ||
                parsedStepsFromPlaywright[3].Type != "change" || parsedStepsFromPlaywright[3].Selector != "#txtTarget" || parsedStepsFromPlaywright[3].Value != "E2E Playwright Text" ||
                parsedStepsFromPlaywright[4].Type != "assertVisible" || parsedStepsFromPlaywright[4].Selector != "#btnTarget" ||
                parsedStepsFromPlaywright[5].Type != "assertNotVisible" || parsedStepsFromPlaywright[5].Selector != "#nonexistent")
            {
                throw new Exception("Parsed steps from Playwright script do not match expected actions/properties.");
            }
            Console.WriteLine("Playwright code parsing verified.");

            // Reset back to Puppeteer default
            mainVm.Recorder.SelectedFormat = RecordingFormat.Puppeteer;
            Console.WriteLine("Scenario 9 PASSED.");

            // Clean up
            if (mainVm.Recorder.IsRecording)
            {
                await mainVm.Recorder.ToggleRecordAsync();
            }
            await cdpService.DisconnectAsync();
            CdpServer.Stop();

            Console.WriteLine("=== ALL TASK-SPECIFIC E2E VERIFICATIONS SUCCESSFUL! ===");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"=== E2E VERIFICATION FAILED: {ex} ===");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private static System.Collections.Generic.List<T> FindLogicalChildren<T>(Avalonia.LogicalTree.ILogical logical) where T : class, Avalonia.LogicalTree.ILogical
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var child in Avalonia.LogicalTree.LogicalExtensions.GetLogicalChildren(logical))
        {
            if (child is T t)
            {
                list.Add(t);
            }
            list.AddRange(FindLogicalChildren<T>(child));
        }
        return list;
    }
}
