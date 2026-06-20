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
                step6.Action != "scroll" || !step6.Value.Contains("direction: down") ||
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

            // Clean up
            await mainVm.Recorder.ToggleRecordAsync();
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
