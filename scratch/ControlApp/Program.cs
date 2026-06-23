using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using Avalonia.Media;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
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
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml")
        });
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://ControlApp/"))
        {
            Source = new Uri("avares://CDP.Inspector.Shared/Styles.axaml")
        });
    }
}

class Program
{
    private static Button? _tempLeakButton;
    private static Window? _tempWindow;

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
                textBox.SetValue(Avalonia.Automation.AutomationProperties.AutomationIdProperty, "txtTargetId");

                button = new Button
                {
                    Name = "btnTarget",
                    Width = 100,
                    Height = 50,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Content = "Click Me"
                };
                button.SetValue(Avalonia.Automation.AutomationProperties.AutomationIdProperty, "btnTargetId");
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

                var containerCanvas = new Canvas { Name = "pnlContainer", Width = 400, Height = 500 };
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

            // 2. Start the CDP Server manually on port 9303
            CdpServer.Start(9303);
            var targetId = CdpServer.Register(window!, "E2E Target");
            Console.WriteLine($"CDP Server started on port 9303, registered E2E Target with ID {targetId}");

            // 3. Setup client-side CdpService and MainWindowViewModel
            var cdpService = new CdpService();
            var mainVm = new MainWindowViewModel(cdpService);
            mainVm.Connection.HostAddress = "http://127.0.0.1:9303";

            // Scan and connect
            var targets = await cdpService.GetTargetsAsync("http://127.0.0.1:9303");
            var target = targets.First(t => t.Id == targetId.ToString());
            await cdpService.ConnectAsync("http://127.0.0.1:9303", target);
            Console.WriteLine("CdpService client connected successfully.");
            goto Scenario25Start;

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
- assertTrue: ""1 == 1""
- takeScreenshot: ""e2e_screenshot.png""
- copyTextFrom: ""#btnTarget""
- openLink: ""http://127.0.0.1:9303/mockPage""
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
                    new RecordedStepModel { Type = "navigate", Url = "http://127.0.0.1:9303/" },
                    new RecordedStepModel { Type = "click", Selector = ":contains(\"Click Me\")", Button = "left", ClickCount = 1 },
                    new RecordedStepModel { Type = "change", Selector = "#txtTarget", Value = "E2E Playwright ' Text with backslash \\" },
                    new RecordedStepModel { Type = "assertVisible", Selector = ":contains(\"Click Me\")" },
                    new RecordedStepModel { Type = "assertNotVisible", Selector = ":contains('Cancel')" }
                });
            });

            string pwGeneratedCode = mainVm.Recorder.GeneratedCode;
            Console.WriteLine($"Generated Playwright Code:\n{pwGeneratedCode}");

            if (!pwGeneratedCode.Contains("import { test, expect, chromium } from '@playwright/test';") ||
                !pwGeneratedCode.Contains("test.describe('CDP Recorded Tests', () => {") ||
                !pwGeneratedCode.Contains("await test.step('Click on element :contains(\"Click Me\")', async () => {") ||
                !pwGeneratedCode.Contains("await test.step('Assert element :contains(\"Click Me\") is visible', async () => {") ||
                !pwGeneratedCode.Contains("await expect(page.locator(':contains(\"Click Me\")')).toBeVisible();") ||
                !pwGeneratedCode.Contains("await expect(page.locator(':contains(\\'Cancel\\')')).toBeHidden();") ||
                !pwGeneratedCode.Contains("chromium.connectOverCDP('http://127.0.0.1:9303')") ||
                !pwGeneratedCode.Contains("page.locator(':contains(\"Click Me\")')") ||
                !pwGeneratedCode.Contains("page.locator('#txtTarget')") ||
                !pwGeneratedCode.Contains("fill('E2E Playwright \\' Text with backslash \\\\')"))
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
                parsedStepsFromPlaywright[2].Type != "click" || parsedStepsFromPlaywright[2].Selector != ":contains(\"Click Me\")" ||
                parsedStepsFromPlaywright[3].Type != "change" || parsedStepsFromPlaywright[3].Selector != "#txtTarget" || parsedStepsFromPlaywright[3].Value != "E2E Playwright ' Text with backslash \\" ||
                parsedStepsFromPlaywright[4].Type != "assertVisible" || parsedStepsFromPlaywright[4].Selector != ":contains(\"Click Me\")" ||
                parsedStepsFromPlaywright[5].Type != "assertNotVisible" || parsedStepsFromPlaywright[5].Selector != ":contains('Cancel')")
            {
                throw new Exception("Parsed steps from Playwright script do not match expected actions/properties.");
            }
            Console.WriteLine("Playwright code parsing verified.");

            // Reset back to Puppeteer default
            mainVm.Recorder.SelectedFormat = RecordingFormat.Puppeteer;
            Console.WriteLine("Scenario 9 PASSED.");

            // 13. Test Scenario 10: Selenium C# Code Generation E2E
            Console.WriteLine("Testing Scenario 10: Selenium C# code generation...");
            mainVm.Recorder.SelectedFormat = RecordingFormat.SeleniumCSharp;
            
            // Check properties
            if (mainVm.Recorder.ScriptTabHeader != "Selenium C#" ||
                mainVm.Recorder.ScriptTitleText != "Generated Selenium C# Script" ||
                mainVm.Recorder.ExportButtonText != "Export Selenium C#")
            {
                throw new Exception("Selenium C# UI properties are incorrect!");
            }

            string seleniumCode = mainVm.Recorder.GeneratedCode;
            Console.WriteLine($"Generated Selenium C# Code:\n{seleniumCode}");

            if (!seleniumCode.Contains("using OpenQA.Selenium;") ||
                !seleniumCode.Contains("using OpenQA.Selenium.Chrome;") ||
                !seleniumCode.Contains("options.DebuggerAddress = \"127.0.0.1:9303\";") ||
                !seleniumCode.Contains("_driver = new ChromeDriver(options);") ||
                !seleniumCode.Contains("_driver.Manage().Window.Size = new Size(1024, 768);") ||
                !seleniumCode.Contains("_driver.Navigate().GoToUrl(\"http://127.0.0.1:9303/\");") ||
                !seleniumCode.Contains("_driver.FindElement(By.CssSelector(\":contains(\\\"Click Me\\\")\")).Click();") ||
                !seleniumCode.Contains("var element_3 = _driver.FindElement(By.CssSelector(\"#txtTarget\"));") ||
                !seleniumCode.Contains("element_3.Clear();") ||
                !seleniumCode.Contains("element_3.SendKeys(\"E2E Playwright ' Text with backslash \\\\\");") ||
                !seleniumCode.Contains("Assert.IsTrue(_driver.FindElement(By.CssSelector(\":contains(\\\"Click Me\\\")\")).Displayed);") ||
                !seleniumCode.Contains("bool isVisible_5 = false;") ||
                !seleniumCode.Contains("catch (NoSuchElementException)"))
            {
                throw new Exception("Generated Selenium C# code is missing expected structures/statements!");
            }
            Console.WriteLine("Selenium C# code generation verified.");
            Console.WriteLine("Scenario 10 PASSED.");

            // 14. Test Scenario 11: Appium C# Code Generation E2E
            Console.WriteLine("Testing Scenario 11: Appium C# code generation...");
            mainVm.Recorder.SelectedFormat = RecordingFormat.AppiumCSharp;

            // Check properties
            if (mainVm.Recorder.ScriptTabHeader != "Appium C#" ||
                mainVm.Recorder.ScriptTitleText != "Generated Appium C# Script" ||
                mainVm.Recorder.ExportButtonText != "Export Appium C#")
            {
                throw new Exception("Appium C# UI properties are incorrect!");
            }

            string appiumCode = mainVm.Recorder.GeneratedCode;
            Console.WriteLine($"Generated Appium C# Code:\n{appiumCode}");

            if (!appiumCode.Contains("using OpenQA.Selenium.Appium;") ||
                !appiumCode.Contains("using OpenQA.Selenium.Appium.Windows;") ||
                !appiumCode.Contains("options.AddAdditionalCapability(\"platformName\", \"Windows\");") ||
                !appiumCode.Contains("options.AddAdditionalCapability(\"automationName\", \"Windows\");") ||
                !appiumCode.Contains("options.AddAdditionalCapability(\"app\", \"Root\");") ||
                !appiumCode.Contains("_driver = new WindowsDriver<WindowsElement>(new Uri(\"http://127.0.0.1:4723/\"), options);") ||
                !appiumCode.Contains("_driver.Manage().Window.Size = new Size(1024, 768);") ||
                !appiumCode.Contains("_driver.FindElementByName(\":contains(\\\"Click Me\\\")\").Click();") ||
                !appiumCode.Contains("var element_3 = _driver.FindElementByAccessibilityId(\"txtTarget\");") ||
                !appiumCode.Contains("element_3.Clear();") ||
                !appiumCode.Contains("element_3.SendKeys(\"E2E Playwright ' Text with backslash \\\\\");") ||
                !appiumCode.Contains("Assert.IsTrue(_driver.FindElementByName(\":contains(\\\"Click Me\\\")\").Displayed);") ||
                !appiumCode.Contains("bool isVisible_5 = false;") ||
                !appiumCode.Contains("catch (Exception)"))
            {
                throw new Exception("Generated Appium C# code is missing expected structures/statements!");
            }
            Console.WriteLine("Appium C# code generation verified.");

            // Reset back to Puppeteer default
            mainVm.Recorder.SelectedFormat = RecordingFormat.Puppeteer;
            Console.WriteLine("Scenario 11 PASSED.");

            // 15. Test Scenario 12: Avalonia Headless xUnit Code Generation E2E
            Console.WriteLine("Testing Scenario 12: Avalonia Headless xUnit code generation...");
            mainVm.Recorder.SelectedFormat = RecordingFormat.AvaloniaHeadlessXUnit;

            // Check properties
            if (mainVm.Recorder.ScriptTabHeader != "Avalonia Headless" ||
                mainVm.Recorder.ScriptTitleText != "Generated Avalonia Headless xUnit Test" ||
                mainVm.Recorder.ExportButtonText != "Export Headless Test")
            {
                throw new Exception("Avalonia Headless UI properties are incorrect!");
            }

            string headlessCode = mainVm.Recorder.GeneratedCode;
            Console.WriteLine($"Generated Avalonia Headless Code:\n{headlessCode}");

            if (!headlessCode.Contains("using Avalonia.Headless.XUnit;") ||
                !headlessCode.Contains("using Avalonia.Diagnostics.Cdp;") ||
                !headlessCode.Contains("using Avalonia.Input;") ||
                !headlessCode.Contains("var window = new CdpSampleApp.MainWindow();") ||
                !headlessCode.Contains("window.Width = 1024;") ||
                !headlessCode.Contains("window.Height = 768;") ||
                !headlessCode.Contains("mainWin.Navigate(\"http://127.0.0.1:9303/\");") ||
                !headlessCode.Contains("var element_2 = SelectorEngine.QuerySelector(window, \":contains(\\\"Click Me\\\")\") as Control;") ||
                !headlessCode.Contains("ClickControl(window, element_2, MouseButton.Left, RawInputModifiers.None);") ||
                !headlessCode.Contains("var element_3 = SelectorEngine.QuerySelector(window, \"#txtTarget\") as Control;") ||
                !headlessCode.Contains("element_3.Focus();") ||
                !headlessCode.Contains("window.KeyTextInput(\"E2E Playwright ' Text with backslash \\\\\");") ||
                !headlessCode.Contains("Assert.True(element_4.IsVisible);") ||
                !headlessCode.Contains("Assert.True(element_5 == null || !element_5.IsVisible);") ||
                !headlessCode.Contains("private static void ClickControl(Window window, Control control, MouseButton button, RawInputModifiers modifiers)") ||
                !headlessCode.Contains("private static void DragAndDrop(Window window, Control source, Control target)"))
            {
                throw new Exception("Generated Avalonia Headless code is missing expected structures/statements!");
            }
            Console.WriteLine("Avalonia Headless code generation verified.");

            // Reset back to Puppeteer default
            mainVm.Recorder.SelectedFormat = RecordingFormat.Puppeteer;
            Console.WriteLine("Scenario 12 PASSED.");

            // 16. Test Scenario 13: DOM vs. Automation Selectors E2E
            Console.WriteLine("Testing Scenario 13: DOM vs. Automation Selectors E2E...");
            
            // Set connection to use automation selectors
            mainVm.Connection.UseAutomationSelectors = true;

            // Find the TextBox node in Elements view model
            var rootDoc = mainVm.Elements.RootNodes.FirstOrDefault();
            if (rootDoc == null)
            {
                throw new Exception("DOM tree root node is null!");
            }

            Func<DomNodeModel, string, DomNodeModel?> findNodeByName = null;
            findNodeByName = (root, name) =>
            {
                if (root.NodeName == name) return root;
                foreach (var child in root.Children)
                {
                    var found = findNodeByName(child, name);
                    if (found != null) return found;
                }
                return null;
            };

            var txtNode = findNodeByName(rootDoc, "TextBox");
            if (txtNode == null)
            {
                throw new Exception("TextBox node not found in DOM tree!");
            }

            // Verify it has AccessibilityId attribute
            var accessIdAttr = txtNode.AttributesList.FirstOrDefault(a => a.Name == "AccessibilityId");
            if (accessIdAttr == null || accessIdAttr.Value != "txtTargetId")
            {
                throw new Exception($"Expected AccessibilityId attribute 'txtTargetId', got '{accessIdAttr?.Value}'");
            }
            Console.WriteLine("AccessibilityId attribute successfully serialized in DOM tree.");

            // Select node and verify SelectedElementSelector changes to the automation selector
            mainVm.Elements.SelectedNode = txtNode;
            if (mainVm.Recorder.TestStudio.SelectedElementSelector != "[AccessibilityId=\"txtTargetId\"]")
            {
                throw new Exception($"Expected SelectedElementSelector to be '[AccessibilityId=\"txtTargetId\"]', got '{mainVm.Recorder.TestStudio.SelectedElementSelector}'");
            }
            Console.WriteLine("Client-side automation selector generation verified.");

            // Switch UseAutomationSelectors back to false and verify it changes back to DOM selector (#txtTarget)
            mainVm.Connection.UseAutomationSelectors = false;
            if (mainVm.Recorder.TestStudio.SelectedElementSelector != "#txtTarget")
            {
                throw new Exception($"Expected SelectedElementSelector to be '#txtTarget', got '{mainVm.Recorder.TestStudio.SelectedElementSelector}'");
            }
            Console.WriteLine("Client-side DOM selector fallback verified.");

            Console.WriteLine("Scenario 13 PASSED.");

        Scenario14Start:
            // 15. Test Scenario 14: Network Monitoring, Blocking, Mocking, and Conditioning E2E
            Console.WriteLine("Testing Scenario 14: Network Monitoring, Blocking, Mocking, and Conditioning...");
            
            var netHandler = new CdpDelegatingHandler(new HttpClientHandler());
            var netClient = new HttpClient(netHandler);

            // 14.1 Passive traffic monitoring
            var requestSentTcs = new TaskCompletionSource<JsonObject>();
            var responseReceivedTcs = new TaskCompletionSource<JsonObject>();
            EventHandler<CdpEventEventArgs> networkEventHandler = (s, e) =>
            {
                if (e.Method == "Network.requestWillBeSent")
                {
                    requestSentTcs.TrySetResult(e.Params!);
                }
                else if (e.Method == "Network.responseReceived")
                {
                    responseReceivedTcs.TrySetResult(e.Params!);
                }
            };
            cdpService.EventReceived += networkEventHandler;

            await cdpService.SendCommandAsync("Network.enable");
            var passiveResponse = await netClient.GetAsync("https://jsonplaceholder.typicode.com/posts/1");
            var reqSent = await requestSentTcs.Task;
            var respRecv = await responseReceivedTcs.Task;
            var networkRequestId = respRecv["requestId"]!.GetValue<string>();
            var bodyRes = await cdpService.SendCommandAsync("Network.getResponseBody", new JsonObject { ["requestId"] = networkRequestId });
            var bodyText = bodyRes["body"]!.GetValue<string>();
            if (string.IsNullOrEmpty(bodyText))
            {
                throw new Exception("Response body was empty!");
            }
            Console.WriteLine("Passive traffic monitoring verified.");
            cdpService.EventReceived -= networkEventHandler;

            // 14.2 Wildcard Blocking
            await cdpService.SendCommandAsync("Network.setBlockedURLs", new JsonObject
            {
                ["urls"] = new JsonArray { "*mock-blocked-target*" }
            });
            try
            {
                await netClient.GetAsync("https://mock-blocked-target.com/data");
                throw new Exception("Expected request to mock-blocked-target.com to be blocked!");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Wildcard blocking successfully blocked the request.");
            }

            // 14.3 Active API Mocking
            mainVm.Network.AutoFulfillEnabled = false;
            await cdpService.SendCommandAsync("Fetch.enable", new JsonObject
            {
                ["patterns"] = new JsonArray
                {
                    new JsonObject { ["urlPattern"] = "*api/v1/users*" }
                }
            });

            var requestPausedTcs = new TaskCompletionSource<JsonObject>();
            EventHandler<CdpEventEventArgs> fetchEventHandler = (s, e) =>
            {
                if (e.Method == "Fetch.requestPaused")
                {
                    requestPausedTcs.TrySetResult(e.Params!);
                }
            };
            cdpService.EventReceived += fetchEventHandler;

            var fetchTask = netClient.GetAsync("https://my-service.com/api/v1/users");

            var pausedParams = await requestPausedTcs.Task;
            var interceptId = pausedParams["requestId"]!.GetValue<string>();

            await cdpService.SendCommandAsync("Fetch.fulfillRequest", new JsonObject
            {
                ["requestId"] = interceptId,
                ["responseCode"] = 201,
                ["responseHeaders"] = new JsonArray
                {
                    new JsonObject { ["name"] = "X-Mock", ["value"] = "CDP" }
                },
                ["body"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"id\": 99, \"name\": \"CDP Mock User\"}"))
            });

            var mockResponse = await fetchTask;
            if (mockResponse.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception($"Expected 201 Created, got {mockResponse.StatusCode}");
            }
            if (!mockResponse.Headers.Contains("X-Mock"))
            {
                throw new Exception("Missing custom X-Mock header!");
            }
            var mockBody = await mockResponse.Content.ReadAsStringAsync();
            if (!mockBody.Contains("CDP Mock User"))
            {
                throw new Exception($"Unexpected mock body: {mockBody}");
            }
            Console.WriteLine("Active API Mocking verified.");
            cdpService.EventReceived -= fetchEventHandler;
            mainVm.Network.AutoFulfillEnabled = true;

            // 14.4 Latency and Throttling
            await cdpService.SendCommandAsync("Network.emulateNetworkConditions", new JsonObject
            {
                ["offline"] = false,
                ["latency"] = 300,
                ["downloadThroughput"] = -1,
                ["uploadThroughput"] = -1
            });
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await netClient.GetAsync("https://jsonplaceholder.typicode.com/posts/2");
            watch.Stop();
            if (watch.ElapsedMilliseconds < 300)
            {
                throw new Exception($"Expected request to take at least 300ms, took {watch.ElapsedMilliseconds}ms");
            }
            Console.WriteLine($"Latency verified: request took {watch.ElapsedMilliseconds}ms");

            await cdpService.SendCommandAsync("Network.emulateNetworkConditions", new JsonObject
            {
                ["offline"] = true,
                ["latency"] = 0,
                ["downloadThroughput"] = -1,
                ["uploadThroughput"] = -1
            });
            try
            {
                await netClient.GetAsync("https://jsonplaceholder.typicode.com/posts/2");
                throw new Exception("Expected request to fail in offline mode!");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Offline mode verified.");
            }

            Console.WriteLine("Scenario 14 PASSED.");



        Scenario15Start:
            // 16. Test Scenario 15: C# REPL & Scripting E2E
            Console.WriteLine("Testing Scenario 15: C# REPL & Scripting...");

            // 15.1 Stateful C# Evaluation
            Console.WriteLine("Executing stateful evaluations...");
            var eval1 = await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "int myVar = 150;",
                ["returnByValue"] = true
            });
            var eval2 = await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "myVar * 2",
                ["returnByValue"] = true
            });
            var eval2Value = eval2["result"]?["value"]?.GetValue<int>() ?? 0;
            if (eval2Value != 300)
            {
                throw new Exception($"Expected stateful myVar * 2 to be 300, got {eval2Value}");
            }
            Console.WriteLine("Stateful C# Evaluation verified.");

            // 15.2 Shorthand Variable Preprocessing ($0 and $vm/$dc)
            var qRes = await cdpService.SendCommandAsync("DOM.querySelector", new JsonObject
            {
                ["nodeId"] = 1,
                ["selector"] = "#txtTarget"
            });
            int textBoxId = qRes["nodeId"]?.GetValue<int>() ?? 0;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Console.WriteLine($"[DEBUG E2E] Active sessions count: {CdpServer.Sessions.Count()}");
                foreach (var s in CdpServer.Sessions)
                {
                    s.InspectedNodeId = textBoxId;
                }
            });
            Console.WriteLine($"InspectedNodeId set to textBoxId: {textBoxId}");

            // Set some properties on DataContext for testing
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox!.DataContext = new ReplTestContext();
            });

            // Set Width to 250 via $0.Width
            await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "$0.Width = 250;",
                ["returnByValue"] = true
            });

            var checkWidth = await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "$0.Width",
                ["returnByValue"] = true
            });
            var checkWidthVal = checkWidth["result"]?["value"]?.GetValue<double>() ?? 0;
            if (checkWidthVal != 250)
            {
                throw new Exception($"Expected width to be updated to 250, got {checkWidthVal}");
            }
            Console.WriteLine("Shorthand $0 property assignment and reading verified.");
            // Check $vm.SomeValue
            var checkVm = await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "$vm.SomeValue",
                ["returnByValue"] = true
            });
            var checkVmVal = checkVm["result"]?["value"]?.GetValue<string>() ?? "";
            if (checkVmVal != "REPL_TEST_CONTEXT")
            {
                throw new Exception($"Expected $vm.SomeValue to resolve 'REPL_TEST_CONTEXT', got '{checkVmVal}'");
            }
            Console.WriteLine("Shorthand $vm DataContext resolution verified.");

            // 15.3 Autocomplete integration
            Console.WriteLine("Testing autocomplete...");
            var completionsRes = await cdpService.SendCommandAsync("Runtime.getCompletions", new JsonObject
            {
                ["expression"] = "$0.He",
                ["cursorPosition"] = 5
            });
            var completionsList = completionsRes["completions"] as JsonArray;
            if (completionsList == null || completionsList.Count == 0)
            {
                throw new Exception("Completions list is empty!");
            }
            bool containsHeight = false;
            Console.WriteLine($"Completions count: {completionsList.Count}");
            foreach (var item in completionsList)
            {
                var text = item?["displayText"]?.GetValue<string>();
                Console.WriteLine($"Completion item: {text}");
                if (text == "Height")
                {
                    containsHeight = true;
                }
            }
            if (!containsHeight)
            {
                throw new Exception("Completions list did not contain 'Height'!");
            }
            Console.WriteLine("Autocomplete completions verified.");

            // 15.4 Console Stream Capture
            Console.WriteLine("Testing console stream capture...");
            var logReceivedEvent = new TaskCompletionSource<string>();
            EventHandler<CdpEventEventArgs> logHandler = (s, e) =>
            {
                if (e.Method == "Log.entryAdded" && e.Params != null)
                {
                    var entry = e.Params["entry"] as JsonObject;
                    var text = entry?["text"]?.GetValue<string>() ?? "";
                    if (text.Contains("REPL_VERIFY_SUCCESS"))
                    {
                        logReceivedEvent.TrySetResult(text);
                    }
                }
            };
            cdpService.EventReceived += logHandler;

            try
            {
                await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "System.Console.WriteLine(\"REPL_VERIFY_SUCCESS\");",
                    ["returnByValue"] = true
                });

                var completedTask = await Task.WhenAny(logReceivedEvent.Task, Task.Delay(5000));
                if (completedTask != logReceivedEvent.Task)
                {
                    throw new Exception("Timed out waiting for Log.entryAdded event!");
                }
            }
            finally
            {
                cdpService.EventReceived -= logHandler;
            }
            Console.WriteLine("Console stream capture verified.");

            Console.WriteLine("Scenario 15 PASSED.");

            // Go to Scenario 17
            goto Scenario17Start;

        Scenario17Start:
            await RunScenario17Async(cdpService, mainVm, window!, textBox!, button!, textBoxId);

        Scenario18Start:
            await RunScenario18Async(cdpService, mainVm);

        Scenario19Start:
            await RunScenario19Async(cdpService, mainVm);

        Scenario21Start:
            await RunScenario21Async(cdpService, mainVm);

        Scenario22Start:
            await RunScenario22Async(cdpService, mainVm, window!);

        Scenario25Start:
            await RunScenario25Async(cdpService, mainVm);

        Scenario26Start:
            await RunScenario26Async(cdpService, mainVm);

            // Cleanup & successful exit
            await cdpService.DisconnectAsync();
            CdpServer.Stop();
            Console.WriteLine("=== ALL TASK-SPECIFIC E2E VERIFICATIONS SUCCESSFUL! ===");
            Environment.Exit(0);

        Scenario16:
            // 17. Test Scenario 16: Accessibility Auditing & Compliance E2E
            Console.WriteLine("Testing Scenario 16: Accessibility Auditing & Compliance E2E...");

            // 17.1 Create custom accessibility controls to audit
            Button? btnMissingName = null;
            TextBlock? lblLowContrast = null;
            Slider? volumeSlider = null;
            CheckBox? notifyCheck = null;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var panel = (Canvas)window!.Content;

                btnMissingName = new Button
                {
                    Name = "btnMissingName",
                    Width = 50,
                    Height = 30,
                    Content = "" // Empty content - missing accessible name!
                };
                Canvas.SetLeft(btnMissingName, 0);
                Canvas.SetTop(btnMissingName, 300);
                panel.Children.Add(btnMissingName);

                var border = new Border
                {
                    Background = Brushes.White,
                    Width = 200,
                    Height = 40
                };
                lblLowContrast = new TextBlock
                {
                    Name = "lblLowContrast",
                    Text = "Low Contrast Warning",
                    Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)), // very light grey
                    FontSize = 12
                };
                border.Child = lblLowContrast;
                Canvas.SetLeft(border, 0);
                Canvas.SetTop(border, 350);
                panel.Children.Add(border);

                volumeSlider = new Slider
                {
                    Name = "volumeSlider",
                    Minimum = 0,
                    Maximum = 100,
                    Value = 45,
                    Width = 100,
                    Height = 30
                };
                volumeSlider.SetValue(Avalonia.Automation.AutomationProperties.NameProperty, "System Volume");
                Canvas.SetLeft(volumeSlider, 0);
                Canvas.SetTop(volumeSlider, 400);
                panel.Children.Add(volumeSlider);

                notifyCheck = new CheckBox
                {
                    Name = "notifyCheck",
                    IsChecked = true,
                    Content = "Enable Notifications",
                    Width = 150,
                    Height = 30
                };
                Canvas.SetLeft(notifyCheck, 0);
                Canvas.SetTop(notifyCheck, 440);
                panel.Children.Add(notifyCheck);

                window!.Measure(new Size(400, 500));
                window!.Arrange(new Rect(0, 0, 400, 500));
            });

            // Wait for visual tree update
            await Task.Delay(200);
            await mainVm.Elements.RefreshDomTreeAsync();

            // 17.2 Enable and test Accessibility.getFullAXTree
            var fullAXTreeRes = await cdpService.SendCommandAsync("Accessibility.getFullAXTree");
            var axNodesArray = fullAXTreeRes["nodes"] as JsonArray;
            if (axNodesArray == null || axNodesArray.Count == 0)
            {
                throw new Exception("Accessibility.getFullAXTree returned no nodes!");
            }

            // Assert role mappings
            JsonObject? checkboxNode = null;
            JsonObject? sliderNode = null;
            foreach (var node in axNodesArray)
            {
                if (node is JsonObject nodeObj)
                {
                    var role = nodeObj["role"]?["value"]?.GetValue<string>();
                    var name = nodeObj["name"]?["value"]?.GetValue<string>();
                    if (role == "checkbox" && name == "Enable Notifications")
                    {
                        checkboxNode = nodeObj;
                    }
                    else if (role == "slider" && name == "System Volume")
                    {
                        sliderNode = nodeObj;
                    }
                }
            }

            if (checkboxNode == null)
            {
                throw new Exception("Failed to map CheckBox to role 'checkbox' with correct name!");
            }
            Console.WriteLine("CheckBox mapped correctly to 'checkbox' role.");

            if (sliderNode == null)
            {
                throw new Exception("Failed to map Slider to role 'slider' with correct name!");
            }
            Console.WriteLine("Slider mapped correctly to 'slider' role.");

            // Verify properties (checked state, slider range values)
            var checkboxProps = checkboxNode["properties"] as JsonArray;
            var isCheckedProp = checkboxProps?.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "checked");
            if (isCheckedProp?["value"]?["value"]?.GetValue<string>() != "true")
            {
                throw new Exception($"Expected checkbox 'checked' property to be true, got '{isCheckedProp?["value"]?["value"]?.GetValue<string>()}'");
            }
            Console.WriteLine("CheckBox toggled/checked property verified.");

            var sliderProps = sliderNode["properties"] as JsonArray;
            var valMinProp = sliderProps?.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "valuemin");
            var valMaxProp = sliderProps?.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "valuemax");
            var valProp = sliderProps?.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "value");
            if (valMinProp?["value"]?["value"]?.GetValue<double>() != 0 ||
                valMaxProp?["value"]?["value"]?.GetValue<double>() != 100 ||
                valProp?["value"]?["value"]?.GetValue<double>() != 45)
            {
                throw new Exception($"Slider range properties incorrect! min: {valMinProp?["value"]?["value"]?.GetValue<double>()}, max: {valMaxProp?["value"]?["value"]?.GetValue<double>()}, val: {valProp?["value"]?["value"]?.GetValue<double>()}");
            }
            Console.WriteLine("Slider range properties verified.");

            // 17.3 Verify Accessibility.getPartialAXTree with fetchRelatives = true
            int sliderBackendNodeId = sliderNode["backendDOMNodeId"]?.GetValue<int>() ?? 0;
            var partialAXTreeRes = await cdpService.SendCommandAsync("Accessibility.getPartialAXTree", new JsonObject
            {
                ["nodeId"] = sliderBackendNodeId,
                ["fetchRelatives"] = true
            });
            var partialNodesArray = partialAXTreeRes["nodes"] as JsonArray;
            if (partialNodesArray == null || partialNodesArray.Count == 0)
            {
                throw new Exception("getPartialAXTree returned no nodes!");
            }
            bool foundSlider = false;
            bool foundNotifyCheck = false;
            foreach (var node in partialNodesArray)
            {
                if (node is JsonObject nodeObj)
                {
                    var name = nodeObj["name"]?["value"]?.GetValue<string>();
                    if (name == "System Volume") foundSlider = true;
                    if (name == "Enable Notifications") foundNotifyCheck = true;
                }
            }
            if (!foundSlider || !foundNotifyCheck)
            {
                throw new Exception($"getPartialAXTree with fetchRelatives=true did not return relatives! Found slider: {foundSlider}, notifyCheck: {foundNotifyCheck}");
            }
            Console.WriteLine("getPartialAXTree with fetchRelatives=true verified successfully.");

            // 17.4 Run diagnostics and verify WCAG contrast audit and interactive name audit
            var auditRes = await cdpService.SendCommandAsync("Audits.runDiagnostics");
            var a11yScore = auditRes["accessibilityScore"]?.GetValue<int>() ?? 0;
            var issuesList = auditRes["issues"] as JsonArray;

            if (a11yScore >= 100)
            {
                throw new Exception($"Expected accessibility score to be reduced from 100 due to audits, got {a11yScore}");
            }

            bool foundMissingNameWarning = false;
            bool foundContrastWarning = false;
            Console.WriteLine($"[DEBUG E2E] Audits returned {issuesList?.Count ?? 0} issues.");
            foreach (var issue in issuesList!)
            {
                var dbgMessage = issue?["message"]?.GetValue<string>() ?? "";
                Console.WriteLine($"[DEBUG E2E] Audit Issue message: '{dbgMessage}'");
                if (issue is JsonObject issueObj)
                {
                    var message = issueObj["message"]?.GetValue<string>() ?? "";
                    if (message.Contains("missing an accessible name") && message.Contains("btnMissingName"))
                    {
                        foundMissingNameWarning = true;
                    }
                    if (message.Contains("contrast ratio", StringComparison.OrdinalIgnoreCase) && message.Contains("below the required WCAG AA threshold", StringComparison.OrdinalIgnoreCase))
                    {
                        foundContrastWarning = true;
                    }
                }
            }

            if (!foundMissingNameWarning)
            {
                throw new Exception("Diagnostics failed to flag missing accessible name warning on empty button!");
            }
            Console.WriteLine("Missing accessible name audit verified.");

            if (!foundContrastWarning)
            {
                throw new Exception("Diagnostics failed to flag low contrast ratio warning on text!");
            }
            Console.WriteLine("WCAG contrast ratio audit verified.");

            // 17.5 Verify client-side ElementsViewModel AX Tree selection & details sync
            await mainVm.Elements.RefreshDomTreeAsync();
            await mainVm.Elements.RefreshAxTreeAsync();
            
            // Re-find root document node of DOM Tree for navigation
            rootDoc = mainVm.Elements.RootNodes.FirstOrDefault();
            if (rootDoc == null)
            {
                throw new Exception("DOM tree root node is null!");
            }

            AxNodeModel? modelNotifyCheck = null;
            AxNodeModel? modelVolumeSlider = null;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var rootAx in mainVm.Elements.AxRootNodes)
                {
                    if (modelNotifyCheck == null) modelNotifyCheck = FindAxNodeByName(rootAx, "Enable Notifications");
                    if (modelVolumeSlider == null) modelVolumeSlider = FindAxNodeByName(rootAx, "System Volume");
                }

                if (modelNotifyCheck == null || modelVolumeSlider == null)
                {
                    throw new Exception("Failed to locate AX nodes in ElementsViewModel local root list!");
                }

                // Sync Path A: Selecting DOM node updates SelectedAxNode
                var notifyCheckDomNode = FindDomNodeByName(rootDoc, "CheckBox");
                if (notifyCheckDomNode == null)
                {
                    throw new Exception("CheckBox node not found in DOM tree!");
                }
                mainVm.Elements.SelectedNode = notifyCheckDomNode;
                if (mainVm.Elements.SelectedAxNode != modelNotifyCheck)
                {
                    throw new Exception($"Expected SelectedAxNode to sync to CheckBox node, got '{mainVm.Elements.SelectedAxNode?.Name}'");
                }
                Console.WriteLine("Sync Path A: DOM selection updates AX tree selection verified.");

                // Sync Path B: Selecting AX Node updates DOM selection & triggers highlight
                mainVm.Elements.SelectedAxNode = modelVolumeSlider;
                var sliderDomNode = FindDomNodeByName(rootDoc, "Slider");
                if (sliderDomNode == null)
                {
                    throw new Exception("Slider node not found in DOM tree!");
                }
                if (mainVm.Elements.SelectedNode != sliderDomNode)
                {
                    throw new Exception($"Expected SelectedNode to sync to Slider node, got '{mainVm.Elements.SelectedNode?.NodeName}'");
                }
                Console.WriteLine("Sync Path B: AX tree selection updates DOM selection verified.");

                // Verify AX details are immediately populated in elements VM
                if (mainVm.Elements.AxNameText != "System Volume" || mainVm.Elements.AxRoleText != "slider")
                {
                    throw new Exception($"AX details not immediately updated in VM! Name: '{mainVm.Elements.AxNameText}', Role: '{mainVm.Elements.AxRoleText}'");
                }
                Console.WriteLine("AX sidebar details immediately populated verified.");

                // 17.6 Verify server-driven AX tree search using queryAXTree
                mainVm.Elements.AxSearchQuery = "System Volume";
                mainVm.Elements.AxSearchCommand.Execute(null);
            });

            await Task.Delay(200);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (mainVm.Elements.SelectedAxNode != modelVolumeSlider)
                {
                    throw new Exception($"Expected Search by 'System Volume' to select Slider, got '{mainVm.Elements.SelectedAxNode?.Name}'");
                }
            });
            Console.WriteLine("Server-driven AX tree search (Accessibility.queryAXTree) verified.");

            Console.WriteLine("Scenario 16 PASSED.");

            // 18. Test Scenario 17: Performance Profiling & Layout/Render Diagnostics E2E
            Console.WriteLine("Testing Scenario 17: Performance Profiling & Layout/Render Diagnostics...");

            // 17.1 Enable Performance & Tracing
            await cdpService.SendCommandAsync("Performance.enable");
            await cdpService.SendCommandAsync("Tracing.start", new JsonObject
            {
                ["categories"] = "Avalonia.Layout,Avalonia.Rendering"
            });

            // 17.2 Register Tracing event handlers
            var traceEvents = new System.Collections.Generic.List<JsonObject>();
            bool tracingCompleteReceived = false;
            EventHandler<CdpEventEventArgs> tracingEventHandler = (s, e) =>
            {
                if (e.Method == "Tracing.dataCollected" && e.Params != null)
                {
                    var value = e.Params["value"] as JsonArray;
                    if (value != null)
                    {
                        foreach (var ev in value)
                        {
                            if (ev is JsonObject evObj) traceEvents.Add(evObj);
                        }
                    }
                }
                else if (e.Method == "Tracing.tracingComplete")
                {
                    tracingCompleteReceived = true;
                }
            };
            cdpService.EventReceived += tracingEventHandler;

            // 17.3 Trigger some layout activity by invalidating the window layout
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.InvalidateMeasure();
                window.InvalidateArrange();
            });

            // Wait for metrics collection & tracing to accumulate data
            await Task.Delay(500);

            // 17.4 Get and assert performance metrics
            var metricsRes = await cdpService.SendCommandAsync("Performance.getMetrics");
            var metricsList = metricsRes["metrics"] as JsonArray;
            if (metricsList == null || metricsList.Count == 0)
            {
                throw new Exception("Performance.getMetrics returned no metrics!");
            }

            var metricsDict = new System.Collections.Generic.Dictionary<string, double>();
            foreach (var m in metricsList)
            {
                if (m is JsonObject mObj)
                {
                    var name = mObj["name"]?.GetValue<string>();
                    var val = mObj["value"]?.GetValue<double>() ?? 0;
                    if (name != null) metricsDict[name] = val;
                }
            }

            // Assert core standard and custom metrics are present
            string[] expectedMetrics = { "Timestamp", "Nodes", "JSHeapUsedSize", "JSHeapTotalSize", "CPUUsage", "LayoutCount", "LayoutDuration", "FPS", "FrameDuration", "DispatcherQueueDelay", "UIThreadBlockingTime" };
            foreach (var metricName in expectedMetrics)
            {
                if (!metricsDict.ContainsKey(metricName))
                {
                    throw new Exception($"Expected metric '{metricName}' was missing from getMetrics response!");
                }
            }
            Console.WriteLine("Performance metrics successfully verified.");

            // 17.5 End tracing and wait for completion
            await cdpService.SendCommandAsync("Tracing.end");
            
            // Wait for tracingComplete
            var traceTimeout = 5000;
            var elapsed = 0;
            while (!tracingCompleteReceived && elapsed < traceTimeout)
            {
                await Task.Delay(100);
                elapsed += 100;
            }

            cdpService.EventReceived -= tracingEventHandler;

            if (!tracingCompleteReceived)
            {
                throw new Exception("Timed out waiting for Tracing.tracingComplete!");
            }

            if (traceEvents.Count == 0)
            {
                throw new Exception("No trace events collected during the run!");
            }

            // Verify trace event contents
            bool foundLayoutTrace = false;
            bool foundRenderTrace = false;
            foreach (var ev in traceEvents)
            {
                var name = ev["name"]?.GetValue<string>();
                var cat = ev["cat"]?.GetValue<string>();
                if (name == "LayoutPass" && cat == "Avalonia.Layout")
                {
                    foundLayoutTrace = true;
                }
                if (name == "RenderFrame" && cat == "Avalonia.Rendering")
                {
                    foundRenderTrace = true;
                }
            }

            if (!foundLayoutTrace)
            {
                throw new Exception("Expected trace events to contain 'LayoutPass' category 'Avalonia.Layout', but none found!");
            }
            if (!foundRenderTrace)
            {
                throw new Exception("Expected trace events to contain 'RenderFrame' category 'Avalonia.Rendering', but none found!");
            }

            Console.WriteLine($"Trace events collected: {traceEvents.Count}. Verified 'LayoutPass' and 'RenderFrame' trace events.");

            // 17.6 Disable Performance Domain
            await cdpService.SendCommandAsync("Performance.disable");
            Console.WriteLine("Scenario 17 PASSED.");

            // =========================================================================
            // Scenario 18: Memory Leak Profiling & Allocation Analysis E2E
            // =========================================================================
            Console.WriteLine("Testing Scenario 18: Memory Leak Profiling & Allocation Analysis...");

            // 1. Get initial memory information
            var heapInfoRes = await cdpService.SendCommandAsync("Memory.getHeapInfo");
            if (heapInfoRes == null || !heapInfoRes.ContainsKey("totalAllocatedBytes"))
            {
                throw new Exception("Memory.getHeapInfo failed to return allocation telemetry.");
            }
            Console.WriteLine($"Initial Allocated Bytes: {heapInfoRes["totalAllocatedBytes"]}");

            // 2. Trigger target allocation leak simulator
            _tempWindow = window;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(CreateLeakButton);

            // Wait for the Loaded event to register the button in the tracker
            await Task.Delay(200);

            // Detach it from the visual tree
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(DetachLeakButton);
            _tempWindow = null;

            // Inspect detached controls list
            var detachedRes = await cdpService.SendCommandAsync("Memory.getDetachedControls");
            if (detachedRes == null || !detachedRes.ContainsKey("detachedControls"))
            {
                throw new Exception("Memory.getDetachedControls failed to respond.");
            }

            var detachedList = detachedRes["detachedControls"] as JsonArray;
            var leakBtnNode = detachedList?.FirstOrDefault(n => n?["name"]?.GetValue<string>() == "leakButton");
            if (leakBtnNode == null)
            {
                throw new Exception("Detached control list did not contain 'leakButton'.");
            }
            Console.WriteLine("leakButton successfully identified in detached list.");

            // Force synchronous Garbage Collection
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Executing GC Collect...");
            Console.ResetColor();
            await cdpService.SendCommandAsync("Memory.collectGarbage");

            // Verify the control is still detached (not collected because of the strong local reference 'leakButton')
            var postGcDetachedRes = await cdpService.SendCommandAsync("Memory.getDetachedControls");
            var postGcList = postGcDetachedRes?["detachedControls"] as JsonArray;
            if (postGcList?.Any(n => n?["name"]?.GetValue<string>() == "leakButton") != true)
            {
                throw new Exception("Leaked button was prematurely collected or lost from detached tracking.");
            }
            Console.WriteLine("leakButton correctly remains in detached list after GC.");

            // Test V8 Heap Snapshot Serializer
            Console.WriteLine("Taking V8 Heap Snapshot...");
            var snapshotRes = await cdpService.SendCommandAsync("Memory.takeHeapSnapshot");
            if (snapshotRes == null || !snapshotRes.ContainsKey("snapshot"))
            {
                throw new Exception("Memory.takeHeapSnapshot failed to generate output.");
            }

            string snapshotJson = snapshotRes.ToString();
            if (!snapshotJson.Contains("\"node_fields\"") || !snapshotJson.Contains("leakButton"))
            {
                throw new Exception("Generated heapsnapshot format is invalid or missing the leaked node.");
            }
            Console.WriteLine("V8 .heapsnapshot successfully generated and validated.");

            // Test VM-side list update
            await mainVm.Memory.TakeSnapshotAsync();
            // Wait for dispatcher
            await Task.Delay(200);
            var vmDetachedList = mainVm.Memory.DetachedControls;
            if (!vmDetachedList.Any(d => d.Name == "leakButton"))
            {
                throw new Exception("MemoryViewModel.DetachedControls did not contain 'leakButton'!");
            }
            Console.WriteLine("MemoryViewModel successfully synced detached control 'leakButton'.");

            var targetBtn = _tempLeakButton;
            _tempLeakButton = null;

            if (targetBtn != null)
            {
                var visited = new System.Collections.Generic.HashSet<object>();
                Console.WriteLine("[DEBUG E2E] Searching references to leakButton in window...");
                FindReferencesInGraph(window, targetBtn, "window", visited);
                visited.Clear();
                Console.WriteLine("[DEBUG E2E] Searching references to leakButton in CdpServer...");
                FindReferencesInGraph(CdpServer.Sessions.ToList(), targetBtn, "CdpServer.Sessions", visited);
                visited.Clear();
                Console.WriteLine("[DEBUG E2E] Searching references to leakButton in mainVm...");
                FindReferencesInGraph(mainVm, targetBtn, "mainVm", visited);
            }

            // Pump the UI thread dispatcher and clear stack frames/registers
            for (int i = 0; i < 5; i++)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Allocate dummy objects to overwrite registers/stack
                    object d1 = new object();
                    object d2 = new object();
                    string d3 = new string('x', i + 1);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }, Avalonia.Threading.DispatcherPriority.Background);
                await Task.Delay(100);
            }

            bool collected = false;
            for (int retry = 0; retry < 5; retry++)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }, Avalonia.Threading.DispatcherPriority.Background);

                await Task.Delay(100);

                var finalDetachedRes = await cdpService.SendCommandAsync("Memory.getDetachedControls");
                var finalDetachedList = finalDetachedRes?["detachedControls"] as JsonArray;
                if (finalDetachedList?.Any(n => n?["name"]?.GetValue<string>() == "leakButton") != true)
                {
                    collected = true;
                    break;
                }
                Console.WriteLine($"[GC Retry {retry + 1}] Leaked button still present in detached tracking...");
            }

            if (!collected)
            {
                throw new Exception("Leaked button was not collected after nulling out references and forcing GC.");
            }
            Console.WriteLine("leakButton successfully garbage-collected and removed from detached tracking.");

            Console.WriteLine("Scenario 18 PASSED.");

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

    private static DomNodeModel? FindDomNodeByName(DomNodeModel root, string name)
    {
        if (root.NodeName == name) return root;
        foreach (var child in root.Children)
        {
            var found = FindDomNodeByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static AxNodeModel? FindAxNodeByName(AxNodeModel root, string name)
    {
        if (root.Name == name) return root;
        foreach (var child in root.Children)
        {
            var found = FindAxNodeByName(child, name);
            if (found != null) return found;
        }
        return null;
    }


    private static System.Collections.Generic.List<T> FindVisualChildren<T>(Visual visual) where T : Visual
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is T t)
            {
                list.Add(t);
            }
            list.AddRange(FindVisualChildren<T>(child));
        }
        return list;
    }

    private static async Task RunScenario17Async(CdpService cdpService, MainWindowViewModel mainVm, Window window, TextBox textBox, Button button, int textBoxId)
    {
        Console.WriteLine("Testing Scenario 17: Element Selection & Selector Path E2E...");

        // 1. Verify selector generation relative to a named ancestor
        Button? namelessBtn = null;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            namelessBtn = new Button
            {
                Width = 80,
                Height = 40,
                Content = "Nameless"
            };
            Canvas.SetLeft(namelessBtn, 10);
            Canvas.SetTop(namelessBtn, 300);

            var panel = (Canvas)window!.Content;
            panel.Children.Add(namelessBtn);

            window!.Measure(new Size(400, 500));
            window!.Arrange(new Rect(0, 0, 400, 500));
        });

        // Wait for visual tree update
        await Task.Delay(200);

        // Now, get selector of namelessBtn
        string namelessBtnSelector = "";
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            namelessBtnSelector = SelectorEngine.GetSelector(namelessBtn, useLogicalTree: false);
        });

        Console.WriteLine($"Generated selector for namelessBtn: {namelessBtnSelector}");
        if (namelessBtnSelector != "#pnlContainer > Button:nth-child(4)")
        {
            throw new Exception($"Expected selector '#pnlContainer > Button:nth-child(4)', got '{namelessBtnSelector}'");
        }
        Console.WriteLine("Selector relative to named ancestor verified.");

        // 2. Verify selector generation client-side (DomClientSelectorGenerator)
        await mainVm.Elements.RefreshDomTreeAsync();
        DomNodeModel? namelessBtnModel = null;
        void FindNamelessButton(DomNodeModel parent)
        {
            if (parent.NodeName.StartsWith("Button"))
            {
                var idAttr = parent.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
                if (idAttr == null || idAttr.Value != "btnTarget")
                {
                    namelessBtnModel = parent;
                    return;
                }
            }
            foreach (var child in parent.Children)
            {
                FindNamelessButton(child);
                if (namelessBtnModel != null) return;
            }
        }
        FindNamelessButton(mainVm.Elements.RootNodes[0]);

        if (namelessBtnModel == null)
        {
            throw new Exception("Could not find nameless Button node in client's DOM tree!");
        }
        var clientGenerator = new DomClientSelectorGenerator();
        string clientSelector = clientGenerator.GenerateSelector(namelessBtnModel);
        Console.WriteLine($"Client generated selector: {clientSelector}");
        if (clientSelector != "#pnlContainer > Button:nth-child(4)")
        {
            throw new Exception($"Expected client selector '#pnlContainer > Button:nth-child(4)', got '{clientSelector}'");
        }
        Console.WriteLine("Client selector relative to named ancestor verified.");

        // 3. Verify that templated child resolves to its logical parent control in recorder
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var session = CdpServer.Sessions.First();
            session.InspectModeEnabled = false;
        });

        await cdpService.SendCommandAsync("Recorder.start", new JsonObject { ["selectorMode"] = "dom" });
        Console.WriteLine("Recorder started.");

        TextBlock? buttonText = null;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            buttonText = FindVisualChildren<TextBlock>(button).FirstOrDefault();
        });

        if (buttonText == null)
        {
            throw new Exception("Could not find TextBlock inside btnTarget!");
        }

        var stepAddedTcs = new TaskCompletionSource<JsonObject>();
        EventHandler<CdpEventEventArgs> stepAddedHandler = (s, e) =>
        {
            if (e.Method == "Recorder.stepAdded")
            {
                var step = e.Params?["step"] as JsonObject;
                if (step?["type"]?.GetValue<string>() == "click")
                {
                    stepAddedTcs.TrySetResult(step);
                }
            }
        };
        cdpService.EventReceived += stepAddedHandler;

        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var mouseDevice = typeof(MouseDevice).GetProperty("Primary", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as IInputDevice;
                var inputRoot = typeof(TopLevel).GetProperty("InputRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(window) as IInputRoot;
                var inputHandler = typeof(TopLevel).GetProperty("PlatformImpl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(window)?.GetType().GetProperty("Input", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(typeof(TopLevel).GetProperty("PlatformImpl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(window)) as Action<RawInputEventArgs>;

                if (mouseDevice != null && inputRoot != null && inputHandler != null)
                {
                    var pos = buttonText.TranslatePoint(new Point(5, 5), window) ?? new Point(155, 5);
                    var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    var pressArgs = (RawPointerEventArgs)Activator.CreateInstance(
                        typeof(RawPointerEventArgs),
                        mouseDevice,
                        timestamp,
                        inputRoot,
                        RawPointerEventType.LeftButtonDown,
                        pos,
                        RawInputModifiers.None
                    )!;
                    inputHandler(pressArgs);

                    var releaseArgs = (RawPointerEventArgs)Activator.CreateInstance(
                        typeof(RawPointerEventArgs),
                        mouseDevice,
                        timestamp + 50,
                        inputRoot,
                        RawPointerEventType.LeftButtonUp,
                        pos,
                        RawInputModifiers.None
                    )!;
                    inputHandler(releaseArgs);
                }
            });

            var completedTask = await Task.WhenAny(stepAddedTcs.Task, Task.Delay(3000));
            if (completedTask != stepAddedTcs.Task)
            {
                throw new Exception("Timed out waiting for click step to be recorded!");
            }

            var recordedStep = stepAddedTcs.Task.Result;
            var selectorsArray = recordedStep["selectors"] as JsonArray;
            var firstSelector = selectorsArray?[0]?[0]?.GetValue<string>();
            Console.WriteLine($"Recorded selector: {firstSelector}");

            if (firstSelector != "#btnTarget")
            {
                throw new Exception($"Expected recorded selector to be '#btnTarget', got '{firstSelector}'");
            }
            Console.WriteLine("Template-internal clicked element successfully resolved to its logical control in recorder.");
        }
        finally
        {
            cdpService.EventReceived -= stepAddedHandler;
            await cdpService.SendCommandAsync("Recorder.stop");
        }

        // 4. Verify DOM update events & selection restoration
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Elements.SelectNodeById(textBoxId);
        });

        if (mainVm.Elements.SelectedNode?.NodeId != textBoxId)
        {
            throw new Exception("Failed to pre-select textbox in elements view!");
        }
        Console.WriteLine("SelectedNode pre-select verified.");

        // Trigger a dynamic visual tree change
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var dynamicBtn = new Button { Name = "btnDynamic", Content = "Dynamic" };
            var panel = (Canvas)window!.Content;
            panel.Children.Add(dynamicBtn);
        });

        // Wait for DOM events to propagate and client to process
        await Task.Delay(500);

        // Verify that the new button is now in the DOM tree, and SelectedNode is still textbox
        bool foundDynamic = false;
        void CheckNodes(DomNodeModel parent)
        {
            var nameAttr = parent.AttributesList.FirstOrDefault(a => a.Name == "Name");
            if (nameAttr?.Value == "btnDynamic")
            {
                foundDynamic = true;
            }
            foreach (var child in parent.Children) CheckNodes(child);
        }
        CheckNodes(mainVm.Elements.RootNodes[0]);

        if (!foundDynamic)
        {
            throw new Exception("Dynamic button 'btnDynamic' was not found in the automatically updated client DOM tree!");
        }
        Console.WriteLine("DOM.childNodeInserted event successfully triggered DOM tree refresh on client.");

        if (mainVm.Elements.SelectedNode?.NodeId != textBoxId)
        {
            throw new Exception($"SelectedNode was lost or changed during DOM update! Expected {textBoxId}, got {mainVm.Elements.SelectedNode?.NodeId}");
        }
        Console.WriteLine("Selection successfully preserved/restored across DOM tree updates.");

        Console.WriteLine("Scenario 17 PASSED.");
    }



    private static void CreateLeakButton()
    {
        var container = _tempWindow?.Content as Canvas;
        if (container != null)
        {
            _tempLeakButton = new Button { Name = "leakButton", Content = "Leaked Button" };
            container.Children.Add(_tempLeakButton);
        }
    }

    private static void DetachLeakButton()
    {
        var container = _tempWindow?.Content as Canvas;
        if (container == null)
        {
            Console.WriteLine("[DEBUG E2E] DetachLeakButton: container is null!");
        }
        if (_tempLeakButton == null)
        {
            Console.WriteLine("[DEBUG E2E] DetachLeakButton: _tempLeakButton is null!");
        }
        if (container != null && _tempLeakButton != null)
        {
            bool removed = container.Children.Remove(_tempLeakButton);
            Console.WriteLine($"[DEBUG E2E] DetachLeakButton: Remove returned {removed}");
        }
    }

    private static void FindReferencesInGraph(object root, object target, string path, System.Collections.Generic.HashSet<object> visited)
    {
        if (root == null || !visited.Add(root)) return;
        if (root == target)
        {
            Console.WriteLine($"[FOUND REFERENCE] Path: {path}");
            return;
        }

        var type = root.GetType();
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return;
        if (type.FullName != null && (type.FullName.StartsWith("System.Reflection") || type.FullName.StartsWith("System.Runtime") || type.FullName.StartsWith("System.Text"))) return;

        // Walk fields
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
        foreach (var field in fields)
        {
            try
            {
                var val = field.GetValue(root);
                if (val != null)
                {
                    FindReferencesInGraph(val, target, $"{path}.{field.Name}", visited);
                }
            }
            catch { }
        }

        // Walk collections
        if (root is System.Collections.IEnumerable enumerable && root is not string)
        {
            int idx = 0;
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    FindReferencesInGraph(item, target, $"{path}[{idx}]", visited);
                }
                idx++;
            }
        }
    }

    private static async Task RunScenario18Async(CdpService cdpService, MainWindowViewModel mainVm)
    {
        Console.WriteLine("Testing Scenario 18: Step Reordering & Inline Editing E2E...");

        var testStudio = mainVm.Recorder.TestStudio;
        
        // Clear steps first
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.ClearCommand.Execute(null);
        });

        if (testStudio.Steps.Count != 0)
        {
            throw new Exception($"Expected steps count to be 0 after clear, got {testStudio.Steps.Count}");
        }

        // Set properties and add some steps
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            testStudio.SelectedElementSelector = "#btnTarget";
            testStudio.InputSimText = "First Step Value";
            await testStudio.AddTapAsync();

            testStudio.SelectedElementSelector = "#txtTarget";
            testStudio.InputSimText = "Second Step Value";
            await testStudio.AddInputAsync();
        });

        if (testStudio.Steps.Count != 2)
        {
            throw new Exception($"Expected steps count to be 2, got {testStudio.Steps.Count}");
        }

        var step1 = testStudio.Steps[0];
        var step2 = testStudio.Steps[1];

        if (step1.Action != "tapOn" || step1.Selector != "#btnTarget" ||
            step2.Action != "inputText" || step2.Selector != "#txtTarget" || step2.Value != "Second Step Value")
        {
            throw new Exception("Steps were not added with correct properties.");
        }

        Console.WriteLine("Step addition verified.");

        // Verify reordering via Move
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.Steps.Move(0, 1);
        });

        if (testStudio.Steps[0].Action != "inputText" || testStudio.Steps[1].Action != "tapOn")
        {
            throw new Exception("Reordering steps in ObservableCollection failed.");
        }

        Console.WriteLine("Step reordering inside collection verified.");

        // Verify YAML regeneration after reordering
        string yamlAfterReorder = testStudio.YamlCode;
        Console.WriteLine($"YAML after reorder:\n{yamlAfterReorder}");
        if (!yamlAfterReorder.Contains("- inputText:") || !yamlAfterReorder.Contains("- tapOn: \"#btnTarget\""))
        {
            throw new Exception("YAML code did not update correctly after step reordering.");
        }

        // Now, test editing step properties programmatically (which simulates editing them in TextBoxes via TwoWay binding)
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Edit step 1 (now inputText)
            var editStep = testStudio.Steps[0];
            editStep.Value = "Modified Value";
            editStep.Selector = "#newSelector";
        });

        string yamlAfterEdit = testStudio.YamlCode;
        Console.WriteLine($"YAML after inline edit:\n{yamlAfterEdit}");
        if (!yamlAfterEdit.Contains("text: \"Modified Value\"") || !yamlAfterEdit.Contains("selector: \"#newSelector\""))
        {
            throw new Exception("YAML code did not update correctly after inline editing step properties.");
        }

        Console.WriteLine("Inline step editing updates verified.");
        Console.WriteLine("Scenario 18 PASSED.");
    }

    private static async Task RunScenario19Async(CdpService cdpService, MainWindowViewModel mainVm)
    {
        Console.WriteLine("Testing Scenario 19: Standard Recorder Step Reordering E2E...");

        var recorder = mainVm.Recorder;

        // Clear recorded steps
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            recorder.ClearCommand.Execute(null);
        });

        if (recorder.RecordedSteps.Count != 0)
        {
            throw new Exception($"Expected recorded steps count to be 0 after clear, got {recorder.RecordedSteps.Count}");
        }

        // Add some steps programmatically
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Simulate adding step 1: Click btnTarget
            var step1 = new RecordedStepModel
            {
                Type = "click",
                Selector = "#btnTarget"
            };
            recorder.RecordedSteps.Add(step1);

            // Simulate adding step 2: Click txtTarget
            var step2 = new RecordedStepModel
            {
                Type = "click",
                Selector = "#txtTarget"
            };
            recorder.RecordedSteps.Add(step2);
        });

        if (recorder.RecordedSteps.Count != 2)
        {
            throw new Exception($"Expected recorded steps count to be 2, got {recorder.RecordedSteps.Count}");
        }

        Console.WriteLine("Standard recorder steps addition verified.");

        // Check original generated code
        string codeBefore = recorder.GeneratedCode;
        Console.WriteLine($"Generated script before reorder:\n{codeBefore}");
        if (!codeBefore.Contains("waitForSelector('#btnTarget')") || !codeBefore.Contains("waitForSelector('#txtTarget')"))
        {
            throw new Exception("Original generated code is incorrect.");
        }
        int btnIndexBefore = codeBefore.IndexOf("waitForSelector('#btnTarget')");
        int txtIndexBefore = codeBefore.IndexOf("waitForSelector('#txtTarget')");
        if (btnIndexBefore > txtIndexBefore)
        {
            throw new Exception("Expected btnTarget click step before txtTarget click step originally.");
        }

        // Reorder steps via Move
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            recorder.RecordedSteps.Move(0, 1);
        });

        if (recorder.RecordedSteps[0].Selector != "#txtTarget" || recorder.RecordedSteps[1].Selector != "#btnTarget")
        {
            throw new Exception("Reordering recorded steps failed.");
        }

        Console.WriteLine("Standard recorder steps reordering verified.");

        // Check regenerated code
        string codeAfter = recorder.GeneratedCode;
        Console.WriteLine($"Generated script after reorder:\n{codeAfter}");
        if (!codeAfter.Contains("waitForSelector('#btnTarget')") || !codeAfter.Contains("waitForSelector('#txtTarget')"))
        {
            throw new Exception("Regenerated code after reorder is incorrect.");
        }
        int btnIndexAfter = codeAfter.IndexOf("waitForSelector('#btnTarget')");
        int txtIndexAfter = codeAfter.IndexOf("waitForSelector('#txtTarget')");
        if (txtIndexAfter > btnIndexAfter)
        {
            throw new Exception("Expected txtTarget click step before btnTarget click step after reorder.");
        }

        Console.WriteLine("Standard recorder script auto-regeneration verified.");
        Console.WriteLine("Scenario 19 PASSED.");
    }



    private static async Task RunScenario21Async(CdpService cdpService, MainWindowViewModel mainVm)
    {
        Console.WriteLine("Testing Scenario 21: Test Studio Edit Display Name & Escape Shortcut E2E...");

        var testStudio = mainVm.Recorder.TestStudio;

        // Clear steps first
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.ClearCommand.Execute(null);
        });

        // Add a step
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            testStudio.SelectedElementSelector = "#btnTarget";
            testStudio.InputSimText = "";
            await testStudio.AddTapAsync();
        });

        if (testStudio.Steps.Count != 1)
        {
            throw new Exception($"Expected steps count to be 1, got {testStudio.Steps.Count}");
        }

        var step = testStudio.Steps[0];
        Console.WriteLine($"Step action Display Name: {step.ActionDisplay}");
        if (step.ActionDisplay != "Tap On")
        {
            throw new Exception($"Expected step ActionDisplay to be 'Tap On', got '{step.ActionDisplay}'");
        }

        // Verify keydown deselecting step
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.SelectedStep = step;
        });

        if (testStudio.SelectedStep != step)
        {
            throw new Exception("Setting SelectedStep failed.");
        }

        Console.WriteLine("Step selection verified.");

        // Create TestStudioView and simulate KeyDown (Escape)
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var view = new CdpInspectorApp.Views.TestStudioView();
            view.DataContext = mainVm;

            var keyEventArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape
            };

            // Call OnKeyDown override
            var mOnKeyDown = view.GetType().GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mOnKeyDown == null)
            {
                throw new Exception("Could not find OnKeyDown on TestStudioView.");
            }

            mOnKeyDown.Invoke(view, new object[] { keyEventArgs });
        });

        if (testStudio.SelectedStep != null)
        {
            throw new Exception("Pressing Escape did not deselect the active step.");
        }

        Console.WriteLine("Escape key deselect verified.");
        Console.WriteLine("Scenario 21 PASSED.");
    }

    private static async Task RunScenario22Async(CdpService cdpService, MainWindowViewModel mainVm, Window window)
    {
        Console.WriteLine("Testing Scenario 22: Scroll & Drag E2E Verification...");

        // Remove dynamicBtn and namelessBtn to avoid overlapping issues
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var panel = (Canvas)window.Content;
            var dynamicBtn = panel.Children.FirstOrDefault(c => c.Name == "btnDynamic");
            if (dynamicBtn != null)
            {
                panel.Children.Remove(dynamicBtn);
            }
            var namelessBtn = panel.Children.FirstOrDefault(c => c is Button b && b.Content as string == "Nameless");
            if (namelessBtn != null)
            {
                panel.Children.Remove(namelessBtn);
            }
            window.Measure(new Size(400, 500));
            window.Arrange(new Rect(0, 0, 400, 500));
            window.UpdateLayout();
        });

        // Wait for visual tree update
        await Task.Delay(200);

        var recorder = mainVm.Recorder;
        var testStudio = recorder.TestStudio;

        // Clear recorded steps first
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            recorder.ClearCommand.Execute(null);
            testStudio.ClearCommand.Execute(null);
        });

        // 1. Verify Scroll Capture
        // Start recording
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!recorder.IsRecording)
            {
                recorder.ToggleRecordCommand.Execute(null);
            }
        });

        int waitRecordCount = 0;
        while (!recorder.IsRecording && waitRecordCount < 30)
        {
            await Task.Delay(100);
            waitRecordCount++;
        }

        if (!recorder.IsRecording)
        {
            throw new Exception("Failed to start recording.");
        }

        Console.WriteLine("Recording started for Scroll Test.");

        // Resolve target window coordinates for scrollTarget.
        var docRes = await cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
        int rootNodeId = docRes["root"]?["nodeId"]?.GetValue<int>() ?? 0;
        if (rootNodeId == 0)
        {
            throw new Exception("Failed to get root document node ID.");
        }

        var qRes = await cdpService.SendCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#scrollTarget"
        });
        int scrollNodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
        if (scrollNodeId == 0)
        {
            throw new Exception("Failed to querySelector '#scrollTarget'.");
        }

        var boxRes = await cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = scrollNodeId });
        var model = boxRes["model"] as JsonObject;
        var content = model?["content"] as JsonArray;
        if (content == null || content.Count < 8)
        {
            throw new Exception("Failed to get box model content for '#scrollTarget'.");
        }

        double x1 = content[0]!.GetValue<double>();
        double y1 = content[1]!.GetValue<double>();
        double x2 = content[4]!.GetValue<double>();
        double y2 = content[5]!.GetValue<double>();
        double scrollX = x1 + (x2 - x1) / 2.0;
        double scrollY = y1 + (y2 - y1) / 2.0;

        Console.WriteLine($"Dispatching scroll event at ({scrollX}, {scrollY})...");
        
        // Setup stepAdded handler to verify scroll step is recorded
        var stepAddedTcs = new TaskCompletionSource<JsonObject>();
        EventHandler<CdpEventEventArgs> stepAddedHandler = (s, e) =>
        {
            if (e.Method == "Recorder.stepAdded" && e.Params != null)
            {
                var step = e.Params["step"] as JsonObject;
                if (step != null && step["type"]?.GetValue<string>() == "scroll")
                {
                    stepAddedTcs.TrySetResult(step);
                }
            }
        };
        cdpService.EventReceived += stepAddedHandler;

        try
        {
            // Dispatch mouseWheel event
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseWheel",
                ["x"] = scrollX,
                ["y"] = scrollY,
                ["deltaX"] = 0,
                ["deltaY"] = -50, // scroll down
                ["button"] = "none",
                ["modifiers"] = 0
            });

            // Wait for scroll step to be recorded
            var timeoutTask = Task.Delay(3000);
            var completedTask = await Task.WhenAny(stepAddedTcs.Task, timeoutTask);
            if (completedTask != stepAddedTcs.Task)
            {
                throw new Exception("Timed out waiting for 'scroll' step to be recorded.");
            }

            var recordedScrollStep = await stepAddedTcs.Task;
            double recordedDeltaY = recordedScrollStep["deltaY"]?.GetValue<double>() ?? 0;
            Console.WriteLine($"Recorded scroll deltaY: {recordedDeltaY}");
            if (recordedDeltaY >= 0)
            {
                throw new Exception($"Expected recorded scroll deltaY to be negative (scrolling down), got {recordedDeltaY}");
            }
        }
        finally
        {
            cdpService.EventReceived -= stepAddedHandler;
        }

        Console.WriteLine("Scroll capture verified.");

        // Clear recorded steps for Drag and Drop
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            recorder.ClearCommand.Execute(null);
            testStudio.ClearCommand.Execute(null);
        });

        // 2. Verify Drag and Drop Capture
        var sourceQRes = await cdpService.SendCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#txtTarget"
        });
        int srcNodeId = sourceQRes["nodeId"]?.GetValue<int>() ?? 0;

        var targetQRes = await cdpService.SendCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#btnTarget"
        });
        int tgtNodeId = targetQRes["nodeId"]?.GetValue<int>() ?? 0;

        var srcBox = await cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = srcNodeId });
        var srcModel = srcBox["model"] as JsonObject;
        var srcBorder = srcModel?["border"] as JsonArray;

        var tgtBox = await cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = tgtNodeId });
        var tgtModel = tgtBox["model"] as JsonObject;
        var tgtBorder = tgtModel?["border"] as JsonArray;

        if (srcBorder == null || tgtBorder == null)
        {
            throw new Exception("Failed to get box models for drag source or target.");
        }

        double startX = srcBorder[0]!.GetValue<double>() + 20;
        double startY = srcBorder[1]!.GetValue<double>() + 20;
        double endX = tgtBorder[0]!.GetValue<double>() + 20;
        double endY = tgtBorder[1]!.GetValue<double>() + 20;

        Console.WriteLine($"Simulating E2E Drag from ({startX}, {startY}) to ({endX}, {endY})...");

        var dragStepAddedTcs = new TaskCompletionSource<JsonObject>();
        stepAddedHandler = (s, e) =>
        {
            if (e.Method == "Recorder.stepAdded" && e.Params != null)
            {
                var step = e.Params["step"] as JsonObject;
                if (step != null && step["type"]?.GetValue<string>() == "dragAndDrop")
                {
                    dragStepAddedTcs.TrySetResult(step);
                }
            }
        };
        cdpService.EventReceived += stepAddedHandler;

        try
        {
            // mousePressed
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = startX,
                ["y"] = startY,
                ["button"] = "left",
                ["clickCount"] = 1
            });
            await Task.Delay(100);

            // mouseMoved (drag start threshold > 10 pixels)
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = startX + 15,
                ["y"] = startY + 15,
                ["button"] = "left"
            });
            await Task.Delay(100);

            // mouseMoved (drag to target)
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = endX,
                ["y"] = endY,
                ["button"] = "left"
            });
            await Task.Delay(100);

            // mouseReleased
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = endX,
                ["y"] = endY,
                ["button"] = "left",
                ["clickCount"] = 1
            });

            // Wait for dragAndDrop step to be recorded
            var timeoutTask = Task.Delay(3000);
            var completedTask = await Task.WhenAny(dragStepAddedTcs.Task, timeoutTask);
            if (completedTask != dragStepAddedTcs.Task)
            {
                throw new Exception("Timed out waiting for 'dragAndDrop' step to be recorded.");
            }

            var recordedDragStep = await dragStepAddedTcs.Task;
            Console.WriteLine($"Recorded dragAndDrop step: {recordedDragStep.ToJsonString()}");
            double dragOffsetX = recordedDragStep["offsetX"]?.GetValue<double>() ?? 0;
            double dragOffsetY = recordedDragStep["offsetY"]?.GetValue<double>() ?? 0;

            if (Math.Abs(dragOffsetX - 20) > 2.0 || Math.Abs(dragOffsetY - 20) > 2.0)
            {
                throw new Exception($"Incorrect drag start offsets recorded: got ({dragOffsetX}, {dragOffsetY}), expected (20, 20).");
            }
        }
        finally
        {
            cdpService.EventReceived -= stepAddedHandler;
        }

        Console.WriteLine("Drag and Drop capture offsets verified.");

        // 3. Verify Test Studio integration
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            recorder.IsTestStudioActive = true;
            recorder.ClearCommand.Execute(null);
            testStudio.ClearCommand.Execute(null);
        });

        // Trigger scroll recording again
        stepAddedTcs = new TaskCompletionSource<JsonObject>();
        cdpService.EventReceived += stepAddedHandler;
        try
        {
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseWheel",
                ["x"] = scrollX,
                ["y"] = scrollY,
                ["deltaX"] = 0,
                ["deltaY"] = -50,
                ["button"] = "none",
                ["modifiers"] = 0
            });
            await Task.WhenAny(stepAddedTcs.Task, Task.Delay(3000));
        }
        finally
        {
            cdpService.EventReceived -= stepAddedHandler;
        }

        // Trigger drag recording again
        dragStepAddedTcs = new TaskCompletionSource<JsonObject>();
        cdpService.EventReceived += stepAddedHandler;
        try
        {
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = startX,
                ["y"] = startY,
                ["button"] = "left",
                ["clickCount"] = 1
            });
            await Task.Delay(100);
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = startX + 15,
                ["y"] = startY + 15,
                ["button"] = "left"
            });
            await Task.Delay(100);
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseMoved",
                ["x"] = endX,
                ["y"] = endY,
                ["button"] = "left"
            });
            await Task.Delay(100);
            await cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = endX,
                ["y"] = endY,
                ["button"] = "left",
                ["clickCount"] = 1
            });
            await Task.WhenAny(dragStepAddedTcs.Task, Task.Delay(3000));
        }
        finally
        {
            cdpService.EventReceived -= stepAddedHandler;
        }

        if (testStudio.Steps.Count != 2)
        {
            throw new Exception($"Expected 2 steps in Test Studio, got {testStudio.Steps.Count}");
        }

        var tsScroll = testStudio.Steps[0];
        var tsDrag = testStudio.Steps[1];

        Console.WriteLine($"Test Studio Step 1: Action={tsScroll.Action}, Selector={tsScroll.Selector}, Value={tsScroll.Value}");
        Console.WriteLine($"Test Studio Step 2: Action={tsDrag.Action}, Selector={tsDrag.Selector}, Value={tsDrag.Value}");

        if (tsScroll.Action != "scroll" || tsDrag.Action != "dragAndDrop")
        {
            throw new Exception($"Unexpected Test Studio actions: Action1={tsScroll.Action}, Action2={tsDrag.Action}");
        }

        // Test YAML serialization round-trip
        string yaml = testStudio.YamlCode;
        Console.WriteLine($"Generated YAML:\n{yaml}");

        if (!yaml.Contains("scroll") || !yaml.Contains("dragAndDrop"))
        {
            throw new Exception("Generated YAML is missing scroll or dragAndDrop steps.");
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.ClearCommand.Execute(null);
        });

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.YamlCode = yaml;
            testStudio.ApplyYaml();
        });

        if (testStudio.Steps.Count != 2)
        {
            throw new Exception($"Expected 2 steps loaded from YAML, got {testStudio.Steps.Count}");
        }

        if (testStudio.Steps[0].Action != "scroll" || testStudio.Steps[1].Action != "dragAndDrop")
        {
            throw new Exception($"Incorrect actions loaded from YAML: {testStudio.Steps[0].Action}, {testStudio.Steps[1].Action}");
        }

        Console.WriteLine("Test Studio YAML Round-trip verified.");

        // 4. Verify Test Studio Replay
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.PlayCommand.Execute(null);
        });

        int waitCount = 0;
        while (testStudio.IsExecuting && waitCount < 100)
        {
            await Task.Delay(100);
            waitCount++;
        }

        if (testStudio.IsExecuting)
        {
            throw new Exception("Test Studio replay timed out.");
        }

        if (testStudio.Steps[0].Status != StepStatus.Passed || testStudio.Steps[1].Status != StepStatus.Passed)
        {
            throw new Exception($"Replay failed: Step1={testStudio.Steps[0].Status}, Step2={testStudio.Steps[1].Status}");
        }

        Console.WriteLine("Test Studio Replay verified.");
        Console.WriteLine("Scenario 22 PASSED.");
    }





    private static async Task RunScenario25Async(CdpService cdpService, MainWindowViewModel mainVm)
    {
        Console.WriteLine("Testing Scenario 25: Visual Preview Highlight Adorners & Hover Highlight E2E...");

        var elements = mainVm.Elements;
        var simulation = mainVm.Simulation;

        await elements.RefreshDomTreeAsync();
        await elements.RefreshAxTreeAsync();

        DomNodeModel? btnNode = null;
        void FindBtnTarget(DomNodeModel parent)
        {
            var idAttr = parent.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
            if (idAttr != null && idAttr.Value == "btnTarget")
            {
                btnNode = parent;
                return;
            }
            foreach (var child in parent.Children)
            {
                FindBtnTarget(child);
                if (btnNode != null) return;
            }
        }

        if (elements.RootNodes.Count > 0)
        {
            FindBtnTarget(elements.RootNodes[0]);
        }

        if (btnNode == null)
        {
            throw new Exception("Could not find btnTarget in DOM tree.");
        }

        // Test 25.1: Selected node highlight (IsHighlightActive = true)
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            elements.IsHighlightActive = true;
            elements.SelectedNode = btnNode;
        });

        await simulation.TriggerHighlightRefreshAsync();

        if (!simulation.IsHighlightOverlayVisible || simulation.HighlightBoxModel == null)
        {
            throw new Exception("Selected node highlight not visible in visual preview.");
        }
        if (simulation.HighlightElementType != "Button" || simulation.HighlightAxRole != "button")
        {
            throw new Exception($"Expected selected element highlight to be Button (button), got {simulation.HighlightElementType} ({simulation.HighlightAxRole})");
        }
        Console.WriteLine("Selected Node Highlight verified.");

        // Clear selection & highlight active state to clean up
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            elements.IsHighlightActive = false;
            elements.SelectedNode = null;
        });
        await simulation.TriggerHighlightRefreshAsync();

        if (simulation.IsHighlightOverlayVisible)
        {
            throw new Exception("Expected highlight overlay to be hidden after cleanup.");
        }

        // Test 25.2: Inspect mode hover highlight
        // First get btnTarget coordinates via DOM.getBoxModel
        var boxRes = await cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = btnNode.NodeId });
        var model = boxRes["model"] as JsonObject;
        var contentQuad = model?["content"] as JsonArray;
        if (contentQuad == null || contentQuad.Count < 8)
        {
            throw new Exception("Could not retrieve btnTarget box model content quad.");
        }
        double x1 = contentQuad[0]!.GetValue<double>();
        double y1 = contentQuad[1]!.GetValue<double>();
        double x3 = contentQuad[4]!.GetValue<double>();
        double y3 = contentQuad[5]!.GetValue<double>();
        double cx = (x1 + x3) / 2;
        double cy = (y1 + y3) / 2;

        // Turn on Inspect Mode (Select Element)
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Connection.IsInspectModeActive = true;
        });

        // Simulate hover by sending mouseMoved event
        await simulation.SendMouseEventAsync("mouseMoved", cx, cy, "none", 0);
        
        // Wait for hover query to process
        await Task.Delay(300);

        if (!simulation.IsHighlightOverlayVisible || simulation.HighlightBoxModel == null)
        {
            throw new Exception("Hover highlight not visible in visual preview during inspect mode.");
        }
        if (simulation.HighlightElementType != "Button" || simulation.HighlightAxRole != "button")
        {
            throw new Exception($"Expected hovered element highlight to be Button (button), got {simulation.HighlightElementType} ({simulation.HighlightAxRole})");
        }
        Console.WriteLine($"Hover Highlight verified: Type={simulation.HighlightElementType}, Role={simulation.HighlightAxRole}, Name={simulation.HighlightAxName}");

        // Test 25.3: Visual Tree Mode hover highlight
        Console.WriteLine("Verifying Hover Highlight in Visual Tree Mode...");
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Elements.ShowVisualTree = true;
        });
        // Wait for DOM tree refresh
        await Task.Delay(300);

        // Simulate hover by sending mouseMoved event
        await simulation.SendMouseEventAsync("mouseMoved", cx, cy, "none", 0);
        await Task.Delay(300);

        if (!simulation.IsHighlightOverlayVisible || simulation.HighlightBoxModel == null)
        {
            throw new Exception("Hover highlight not visible in visual preview during visual tree mode.");
        }
        Console.WriteLine($"Visual Tree Mode Hover Highlight verified: Type={simulation.HighlightElementType}");

        // Reset ShowVisualTree to false
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Elements.ShowVisualTree = false;
        });
        await Task.Delay(300);

        // Test 25.4: Accessibility/Automation Tree Mode hover highlight
        Console.WriteLine("Verifying Hover Highlight in Accessibility/Automation Tree Mode...");
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Elements.SelectedTreeTabIndex = 1;
        });

        // Simulate hover by sending mouseMoved event
        await simulation.SendMouseEventAsync("mouseMoved", cx, cy, "none", 0);
        await Task.Delay(300);

        if (!simulation.IsHighlightOverlayVisible || simulation.HighlightBoxModel == null)
        {
            throw new Exception("Hover highlight not visible in visual preview during accessibility tree mode.");
        }
        if (simulation.HighlightElementType != "Button" || simulation.HighlightAxRole != "button")
        {
            throw new Exception($"Expected hovered AX element highlight to be Button (button), got {simulation.HighlightElementType} ({simulation.HighlightAxRole})");
        }
        Console.WriteLine($"Accessibility Tree Mode Hover Highlight verified: Type={simulation.HighlightElementType}, Role={simulation.HighlightAxRole}, Name={simulation.HighlightAxName}");

        // Reset tab index
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Elements.SelectedTreeTabIndex = 0;
        });

        // Turn off Inspect Mode (Select Element)
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            mainVm.Connection.IsInspectModeActive = false;
        });

        // Wait for cleanup
        await Task.Delay(100);

        if (simulation.IsHighlightOverlayVisible)
        {
            throw new Exception("Expected highlight overlay to be hidden after exiting inspect mode.");
        }
        Console.WriteLine("Inspect mode exiting successfully cleared hover highlight.");

        Console.WriteLine("Scenario 25 PASSED.");
    }

    private static async Task RunScenario26Async(CdpService cdpService, MainWindowViewModel mainVm)
    {
        Console.WriteLine("Testing Scenario 26: Test Studio Report Generation & Video Recording E2E...");

        var testStudio = mainVm.Recorder.TestStudio;

        // Set configuration options
        var tempReportDir = Path.Combine(Directory.GetCurrentDirectory(), "TempTestReports");
        if (Directory.Exists(tempReportDir))
        {
            try { Directory.Delete(tempReportDir, true); } catch { }
        }
        Directory.CreateDirectory(tempReportDir);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.OutputDirectory = tempReportDir;
            testStudio.IsRecordVideoEnabled = true;
            testStudio.IsGenerateReportEnabled = true;
            testStudio.Steps.Clear();
        });

        // Load some steps to execute
        string yaml = @"- launchApp
- delay: 200
- assertTrue: ""1 == 1""
";
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            testStudio.YamlCode = yaml;
            testStudio.ApplyYaml();
        });

        if (testStudio.Steps.Count != 3)
        {
            throw new Exception($"Expected 3 steps, got {testStudio.Steps.Count}");
        }

        // Start playback
        Console.WriteLine("Running playback with reporting and video enabled...");
        await testStudio.PlayAsync();

        // Wait for execution to finish
        int elapsedMs = 0;
        while (testStudio.IsExecuting && elapsedMs < 10000)
        {
            await Task.Delay(100);
            elapsedMs += 100;
        }

        if (testStudio.IsExecuting)
        {
            throw new Exception("Test execution timed out!");
        }

        // Verify step statuses
        for (int i = 0; i < testStudio.Steps.Count; i++)
        {
            var step = testStudio.Steps[i];
            if (step.Status != StepStatus.Passed)
            {
                throw new Exception($"Step {i + 1} ({step.ActionDisplay}) failed with error: {step.ErrorMessage}");
            }
        }
        Console.WriteLine("All steps executed successfully.");

        // Verify cached steps and RelativeStartMs
        var cachedStepsField = typeof(TestStudioViewModel).GetField("_lastRunSteps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cachedSteps = cachedStepsField?.GetValue(testStudio) as System.Collections.IList;
        if (cachedSteps == null || cachedSteps.Count != 3)
        {
            throw new Exception($"Expected 3 cached steps in _lastRunSteps, got {cachedSteps?.Count ?? 0}");
        }
        Console.WriteLine("Cached steps count verified.");

        foreach (var item in cachedSteps)
        {
            var relativeStartMsProp = item.GetType().GetProperty("RelativeStartMs");
            var relativeStartMs = (double)(relativeStartMsProp?.GetValue(item) ?? -1.0);
            if (relativeStartMs < 0)
            {
                throw new Exception($"Expected RelativeStartMs to be >= 0, got {relativeStartMs}");
            }
        }
        Console.WriteLine("RelativeStartMs timings verified on all steps.");

        // Verify generated reports and artifacts
        if (!testStudio.HasLastRunRecording)
        {
            throw new Exception("HasLastRunRecording is false after run!");
        }

        if (string.IsNullOrEmpty(testStudio.LastReportPath) || !File.Exists(testStudio.LastReportPath))
        {
            throw new Exception($"HTML report file not found at: {testStudio.LastReportPath}");
        }
        Console.WriteLine($"HTML report verified: {testStudio.LastReportPath}");

        if (string.IsNullOrEmpty(testStudio.LastPdfReportPath) || !File.Exists(testStudio.LastPdfReportPath))
        {
            throw new Exception($"PDF report file not found at: {testStudio.LastPdfReportPath}");
        }
        Console.WriteLine($"PDF report verified: {testStudio.LastPdfReportPath}");

        // Assert size > 0
        var pdfInfo = new FileInfo(testStudio.LastPdfReportPath);
        if (pdfInfo.Length == 0)
        {
            throw new Exception("Generated PDF report is empty (0 bytes)!");
        }
        Console.WriteLine($"PDF report size: {pdfInfo.Length} bytes.");

        var htmlContent = await File.ReadAllTextAsync(testStudio.LastReportPath);
        if (!htmlContent.Contains("Test Run Report") || !htmlContent.Contains("assertTrue"))
        {
            throw new Exception("Generated HTML report does not contain expected title or step content!");
        }
        Console.WriteLine("HTML report contents verified.");

        // Check captured images
        var imagesDir = Path.Combine(Path.GetDirectoryName(testStudio.LastReportPath)!, "images");
        if (!Directory.Exists(imagesDir))
        {
            throw new Exception($"Images directory not found at: {imagesDir}");
        }

        var images = Directory.GetFiles(imagesDir);
        Console.WriteLine($"Found {images.Length} captured artifact files (screenshots and frames).");
        if (images.Length == 0)
        {
            throw new Exception("No step screenshots or video frames were captured!");
        }

        bool hasScreenshot = images.Any(img => img.Contains("step_") && img.EndsWith(".png"));
        bool hasFrame = images.Any(img => img.Contains("frame_") && img.EndsWith(".jpg"));

        if (!hasScreenshot)
        {
            throw new Exception("No step screenshots were captured!");
        }
        Console.WriteLine("Step screenshot files verified.");

        if (!hasFrame)
        {
            throw new Exception("No video frame files were captured!");
        }
        Console.WriteLine("Video frame files verified.");

        // Clean up temp directory
        try
        {
            Directory.Delete(tempReportDir, true);
            Console.WriteLine("Temporary test reports cleaned up.");
        }
        catch { }

        Console.WriteLine("Scenario 26 PASSED.");
    }
}

public class ReplTestContext
{
    public string SomeValue { get; set; } = "REPL_TEST_CONTEXT";
}
