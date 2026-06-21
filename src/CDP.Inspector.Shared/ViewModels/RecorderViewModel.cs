using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Media;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public enum RecordingFormat
{
    Puppeteer,
    PlaywrightTest,
    SeleniumCSharp,
    AppiumCSharp,
    AvaloniaHeadlessXUnit
}

public class RecorderViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private readonly Func<string> _getHostAddress;
    private readonly Func<bool>? _useAutomationProvider;
    private ObservableCollection<RecordedStepModel> _recordedSteps = new();
    private bool _isRecording;
    private string _generatedCode = "";
    private bool _isReplayEnabled;
    private string _toggleRecordButtonText = "Start Recording";
    private IBrush _toggleRecordButtonBackground = new SolidColorBrush(Color.Parse("#c5221f"));
    private IBrush _toggleRecordButtonBorderBrush = new SolidColorBrush(Color.Parse("#c5221f"));

    public ObservableCollection<RecordedStepModel> RecordedSteps => _recordedSteps;

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isRecording, value))
            {
                if (_isRecording)
                {
                    ToggleRecordButtonText = "Stop Recording";
                    ToggleRecordButtonBackground = Brushes.DarkGray;
                    ToggleRecordButtonBorderBrush = Brushes.DarkGray;
                }
                else
                {
                    ToggleRecordButtonText = "Start Recording";
                    ToggleRecordButtonBackground = new SolidColorBrush(Color.Parse("#c5221f"));
                    ToggleRecordButtonBorderBrush = new SolidColorBrush(Color.Parse("#c5221f"));
                }
            }
        }
    }

    public string ToggleRecordButtonText
    {
        get => _toggleRecordButtonText;
        private set => RaiseAndSetIfChanged(ref _toggleRecordButtonText, value);
    }

    public IBrush ToggleRecordButtonBackground
    {
        get => _toggleRecordButtonBackground;
        private set => RaiseAndSetIfChanged(ref _toggleRecordButtonBackground, value);
    }

    public IBrush ToggleRecordButtonBorderBrush
    {
        get => _toggleRecordButtonBorderBrush;
        private set => RaiseAndSetIfChanged(ref _toggleRecordButtonBorderBrush, value);
    }

    public string GeneratedCode
    {
        get => _generatedCode;
        private set => RaiseAndSetIfChanged(ref _generatedCode, value);
    }

    public bool IsReplayEnabled
    {
        get => _isReplayEnabled;
        private set => RaiseAndSetIfChanged(ref _isReplayEnabled, value);
    }

    private RecordingFormat _selectedFormat = RecordingFormat.Puppeteer;
    public RecordingFormat SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedFormat, value))
            {
                UpdateGeneratedCode();
                OnPropertyChanged(nameof(ScriptTabHeader));
                OnPropertyChanged(nameof(ScriptTitleText));
                OnPropertyChanged(nameof(ExportButtonText));
            }
        }
    }

    public List<RecordingFormat> AvailableFormats { get; } = new()
    {
        RecordingFormat.Puppeteer,
        RecordingFormat.PlaywrightTest,
        RecordingFormat.SeleniumCSharp,
        RecordingFormat.AppiumCSharp,
        RecordingFormat.AvaloniaHeadlessXUnit
    };

    public string ScriptTabHeader => SelectedFormat switch
    {
        RecordingFormat.Puppeteer => "Puppeteer Script",
        RecordingFormat.PlaywrightTest => "Playwright Script",
        RecordingFormat.SeleniumCSharp => "Selenium C#",
        RecordingFormat.AppiumCSharp => "Appium C#",
        RecordingFormat.AvaloniaHeadlessXUnit => "Avalonia Headless",
        _ => ""
    };

    public string ScriptTitleText => SelectedFormat switch
    {
        RecordingFormat.Puppeteer => "Generated Puppeteer Script",
        RecordingFormat.PlaywrightTest => "Generated Playwright Test Script",
        RecordingFormat.SeleniumCSharp => "Generated Selenium C# Script",
        RecordingFormat.AppiumCSharp => "Generated Appium C# Script",
        RecordingFormat.AvaloniaHeadlessXUnit => "Generated Avalonia Headless xUnit Test",
        _ => ""
    };

    public string ExportButtonText => SelectedFormat switch
    {
        RecordingFormat.Puppeteer => "Export Puppeteer",
        RecordingFormat.PlaywrightTest => "Export Playwright",
        RecordingFormat.SeleniumCSharp => "Export Selenium C#",
        RecordingFormat.AppiumCSharp => "Export Appium C#",
        RecordingFormat.AvaloniaHeadlessXUnit => "Export Headless Test",
        _ => ""
    };

    private bool _isTestStudioActive;
    public bool IsTestStudioActive
    {
        get => _isTestStudioActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isTestStudioActive, value))
            {
                Console.WriteLine($"[DEBUG] IsTestStudioActive setter called: new value = {value}");
            }
        }
    }

    public TestStudioViewModel TestStudio { get; }

    public ICommand ToggleRecordCommand { get; }
    public ICommand ReplayCommand { get; }
    public ICommand ClearCommand { get; }

    public RecorderViewModel(ICdpService cdpService, Func<string> getHostAddress, Func<bool>? useAutomationProvider = null)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _getHostAddress = getHostAddress ?? throw new ArgumentNullException(nameof(getHostAddress));
        _useAutomationProvider = useAutomationProvider;

        TestStudio = new TestStudioViewModel(_cdpService);

        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        ToggleRecordCommand = new RelayCommand(async () => await ToggleRecordAsync(), () => _cdpService.IsConnected);
        ReplayCommand = new RelayCommand(async () => await ReplayAsync(), () => _cdpService.IsConnected && RecordedSteps.Count > 0);
        ClearCommand = new RelayCommand(ClearRecording);

        RecordedSteps.CollectionChanged += (sender, args) =>
        {
            UpdateGeneratedCode();
            IsReplayEnabled = RecordedSteps.Count > 0;
            ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();
        };

        UpdateGeneratedCode();
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected)
            {
                ClearData();
            }
            ((RelayCommand)ToggleRecordCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Recorder.stepAdded" && e.Params != null)
        {
            if (TestStudio.IsExecuting) return;

            var step = e.Params["step"] as JsonObject;
            if (step != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddRecordedStep(step);
                });
            }
        }
    }

    public async Task ToggleRecordAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            if (!IsRecording)
            {
                var mode = _useAutomationProvider?.Invoke() == true ? "automation" : "dom";
                await _cdpService.SendCommandAsync("Recorder.start", new JsonObject { ["selectorMode"] = mode });
                IsRecording = true;
            }
            else
            {
                await _cdpService.SendCommandAsync("Recorder.stop");
                IsRecording = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling recording: {ex.Message}");
        }
    }

    private void AddRecordedStep(JsonObject stepJson)
    {
        string type = stepJson["type"]?.GetValue<string>() ?? "";
        Console.WriteLine($"[DEBUG] AddRecordedStep: type={type}, IsTestStudioActive={IsTestStudioActive}");
        string value = stepJson["value"]?.GetValue<string>() ?? "";
        double offsetX = stepJson["offsetX"]?.GetValue<double>() ?? 0;
        double offsetY = stepJson["offsetY"]?.GetValue<double>() ?? 0;
        if (type == "scroll")
        {
            offsetX = stepJson["deltaX"]?.GetValue<double>() ?? 0;
            offsetY = stepJson["deltaY"]?.GetValue<double>() ?? 0;
        }
        double width = stepJson["width"]?.GetValue<double>() ?? 0;
        double height = stepJson["height"]?.GetValue<double>() ?? 0;
        string url = stepJson["url"]?.GetValue<string>() ?? "";
        string keyVal = stepJson["key"]?.GetValue<string>() ?? "";
        string button = stepJson["button"]?.GetValue<string>() ?? "left";
        int clickCount = stepJson["clickCount"]?.GetValue<int>() ?? 1;
        int modifiers = stepJson["modifiers"]?.GetValue<int>() ?? 0;
        double targetOffsetX = stepJson["targetOffsetX"]?.GetValue<double>() ?? 0;
        double targetOffsetY = stepJson["targetOffsetY"]?.GetValue<double>() ?? 0;
        
        string selector = "";
        var selectorsArr = stepJson["selectors"] as JsonArray;
        if (selectorsArr != null && selectorsArr.Count > 0)
        {
            var firstSelectorGroup = selectorsArr[0] as JsonArray;
            if (firstSelectorGroup != null && firstSelectorGroup.Count > 0)
            {
                selector = firstSelectorGroup[0]?.GetValue<string>() ?? "";
            }
        }

        string targetSelector = "";
        var targetSelectorsArr = stepJson["targetSelectors"] as JsonArray;
        if (targetSelectorsArr != null && targetSelectorsArr.Count > 0)
        {
            var firstGroup = targetSelectorsArr[0] as JsonArray;
            if (firstGroup != null && firstGroup.Count > 0)
            {
                targetSelector = firstGroup[0]?.GetValue<string>() ?? "";
            }
        }

        var model = new RecordedStepModel
        {
            Type = type,
            Selector = selector,
            Value = value,
            OffsetX = offsetX,
            OffsetY = offsetY,
            Width = width,
            Height = height,
            Url = url,
            Key = keyVal,
            Button = button,
            ClickCount = clickCount,
            Modifiers = modifiers,
            TargetSelector = targetSelector,
            TargetOffsetX = targetOffsetX,
            TargetOffsetY = targetOffsetY
        };

        RecordedSteps.Add(model);

        if (IsTestStudioActive)
        {
            TestStudioStepModel? tsStep = null;
            if (type == "click")
            {
                tsStep = new TestStudioStepModel
                {
                    Action = "tapOn",
                    Selector = selector,
                    Value = ""
                };
            }
            else if (type == "change")
            {
                tsStep = new TestStudioStepModel
                {
                    Action = "inputText",
                    Selector = selector,
                    Value = value
                };
            }
            else if (type == "navigate")
            {
                tsStep = new TestStudioStepModel
                {
                    Action = "launchApp",
                    Selector = "",
                    Value = ""
                };
            }
            else if (type == "keydown")
            {
                tsStep = new TestStudioStepModel
                {
                    Action = "pressKey",
                    Selector = "",
                    Value = keyVal
                };
            }
            else if (type == "scroll")
            {
                string dir = "down";
                double amt = 0;
                if (Math.Abs(offsetY) >= Math.Abs(offsetX))
                {
                    dir = offsetY < 0 ? "down" : "up";
                    amt = Math.Abs(offsetY);
                }
                else
                {
                    dir = offsetX < 0 ? "right" : "left";
                    amt = Math.Abs(offsetX);
                }

                tsStep = new TestStudioStepModel
                {
                    Action = "scroll",
                    Selector = selector,
                    Value = $"direction: {dir}, amount: {amt}"
                };
            }
            else if (type == "dragAndDrop")
            {
                var parts = new List<string>();
                parts.Add($"targetSelector: {targetSelector}");
                if (offsetX != 0.0 || offsetY != 0.0)
                {
                    parts.Add($"offsetX: {offsetX}");
                    parts.Add($"offsetY: {offsetY}");
                }
                if (targetOffsetX != 0.0 || targetOffsetY != 0.0)
                {
                    parts.Add($"targetOffsetX: {targetOffsetX}");
                    parts.Add($"targetOffsetY: {targetOffsetY}");
                }

                tsStep = new TestStudioStepModel
                {
                    Action = "dragAndDrop",
                    Selector = selector,
                    Value = string.Join(", ", parts)
                };
            }

            if (tsStep != null)
            {
                TestStudio.Steps.Add(tsStep);
            }
        }

        IsReplayEnabled = RecordedSteps.Count > 0;
        ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();

        UpdateGeneratedCode();
    }

    private void UpdateGeneratedCode()
    {
        if (SelectedFormat == RecordingFormat.Puppeteer)
        {
            var generator = new PuppeteerGenerator();
            GeneratedCode = generator.Generate(RecordedSteps, _getHostAddress());
        }
        else if (SelectedFormat == RecordingFormat.PlaywrightTest)
        {
            var generator = new PlaywrightGenerator();
            GeneratedCode = generator.Generate(RecordedSteps, _getHostAddress());
        }
        else if (SelectedFormat == RecordingFormat.SeleniumCSharp)
        {
            var generator = new SeleniumCSharpGenerator();
            GeneratedCode = generator.Generate(RecordedSteps, _getHostAddress());
        }
        else if (SelectedFormat == RecordingFormat.AppiumCSharp)
        {
            var generator = new AppiumCSharpGenerator();
            GeneratedCode = generator.Generate(RecordedSteps, _getHostAddress());
        }
        else if (SelectedFormat == RecordingFormat.AvaloniaHeadlessXUnit)
        {
            var generator = new AvaloniaHeadlessXUnitGenerator();
            GeneratedCode = generator.Generate(RecordedSteps, _getHostAddress());
        }
    }

    public async Task ReplayAsync()
    {
        if (!_cdpService.IsConnected || RecordedSteps.Count == 0) return;

        IsReplayEnabled = false;
        ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();

        bool wasRecording = IsRecording;
        try
        {
            if (wasRecording)
            {
                await _cdpService.SendCommandAsync("Recorder.stop");
            }

            var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
            var root = docRes["root"] as JsonObject;
            int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

            var stepsToReplay = RecordedSteps.ToList();

            foreach (var step in stepsToReplay)
            {
                if (step.Type == "click")
                {
                    var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = step.Selector };
                    var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
                    int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
                    if (nodeId > 0)
                    {
                        var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                        var model = boxRes["model"] as JsonObject;
                        var content = model?["content"] as JsonArray;
                        if (content != null && content.Count >= 8)
                        {
                            double x1 = content[0]!.GetValue<double>();
                            double y1 = content[1]!.GetValue<double>();
                            double x2 = content[4]!.GetValue<double>();
                            double y2 = content[5]!.GetValue<double>();
                            
                            double centerX = x1 + (x2 - x1) / 2.0;
                            double centerY = y1 + (y2 - y1) / 2.0;

                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100);

                            int clickCount = step.ClickCount > 0 ? step.ClickCount : 1;
                            for (int c = 1; c <= clickCount; c++)
                            {
                                var pressParams = new JsonObject
                                {
                                    ["type"] = "mousePressed",
                                    ["x"] = centerX,
                                    ["y"] = centerY,
                                    ["button"] = step.Button,
                                    ["clickCount"] = c,
                                    ["modifiers"] = step.Modifiers
                                };
                                await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", pressParams);
                                await Task.Delay(50);

                                var releaseParams = new JsonObject
                                {
                                    ["type"] = "mouseReleased",
                                    ["x"] = centerX,
                                    ["y"] = centerY,
                                    ["button"] = step.Button,
                                    ["clickCount"] = c,
                                    ["modifiers"] = step.Modifiers
                                };
                                await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", releaseParams);
                                if (c < clickCount) await Task.Delay(50);
                            }
                            await Task.Delay(300);
                        }
                    }
                }
                else if (step.Type == "change")
                {
                    var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = step.Selector };
                    var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
                    int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
                    if (nodeId > 0)
                    {
                        await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                        await Task.Delay(100);

                        await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = step.Value });
                        await Task.Delay(300);
                    }
                }
                else if (step.Type == "setViewport")
                {
                    await _cdpService.SendCommandAsync("Emulation.setDeviceMetricsOverride", new JsonObject
                    {
                        ["width"] = (int)step.Width,
                        ["height"] = (int)step.Height,
                        ["deviceScaleFactor"] = 1,
                        ["mobile"] = false
                    });
                    await Task.Delay(300);
                }
                else if (step.Type == "navigate")
                {
                    await _cdpService.SendCommandAsync("Page.navigate", new JsonObject
                    {
                        ["url"] = step.Url
                    });
                    await Task.Delay(300);
                }
                else if (step.Type == "keydown")
                {
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = step.Key,
                        ["modifiers"] = step.Modifiers
                    });
                    await Task.Delay(50);

                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = step.Key,
                        ["modifiers"] = step.Modifiers
                    });
                    await Task.Delay(150);
                }
                else if (step.Type == "dragAndDrop")
                {
                    var sourceQParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = step.Selector };
                    var sourceQRes = await _cdpService.SendCommandAsync("DOM.querySelector", sourceQParams);
                    int sourceNodeId = sourceQRes["nodeId"]?.GetValue<int>() ?? 0;

                    var targetQParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = step.TargetSelector };
                    var targetQRes = await _cdpService.SendCommandAsync("DOM.querySelector", targetQParams);
                    int targetNodeId = targetQRes["nodeId"]?.GetValue<int>() ?? 0;

                    if (sourceNodeId > 0 && targetNodeId > 0)
                    {
                        var srcBoxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = sourceNodeId });
                        var srcModel = srcBoxRes["model"] as JsonObject;
                        var srcContent = srcModel?["content"] as JsonArray;

                        var tgtBoxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = targetNodeId });
                        var tgtModel = tgtBoxRes["model"] as JsonObject;
                        var tgtContent = tgtModel?["content"] as JsonArray;

                        if (srcContent != null && srcContent.Count >= 8 && tgtContent != null && tgtContent.Count >= 8)
                        {
                            double srcX = (step.OffsetX != 0.0 || step.OffsetY != 0.0) ? srcContent[0]!.GetValue<double>() + step.OffsetX : srcContent[0]!.GetValue<double>() + (srcContent[4]!.GetValue<double>() - srcContent[0]!.GetValue<double>()) / 2.0;
                            double srcY = (step.OffsetX != 0.0 || step.OffsetY != 0.0) ? srcContent[1]!.GetValue<double>() + step.OffsetY : srcContent[1]!.GetValue<double>() + (srcContent[5]!.GetValue<double>() - srcContent[1]!.GetValue<double>()) / 2.0;

                            double tgtX = (step.TargetOffsetX != 0.0 || step.TargetOffsetY != 0.0) ? tgtContent[0]!.GetValue<double>() + step.TargetOffsetX : tgtContent[0]!.GetValue<double>() + (tgtContent[4]!.GetValue<double>() - tgtContent[0]!.GetValue<double>()) / 2.0;
                            double tgtY = (step.TargetOffsetX != 0.0 || step.TargetOffsetY != 0.0) ? tgtContent[1]!.GetValue<double>() + step.TargetOffsetY : tgtContent[1]!.GetValue<double>() + (tgtContent[5]!.GetValue<double>() - tgtContent[1]!.GetValue<double>()) / 2.0;

                            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                            {
                                ["type"] = "mouseMoved",
                                ["x"] = srcX,
                                ["y"] = srcY,
                                ["button"] = "none",
                                ["modifiers"] = step.Modifiers
                            });
                            await Task.Delay(100);

                            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                            {
                                ["type"] = "mousePressed",
                                ["x"] = srcX,
                                ["y"] = srcY,
                                ["button"] = "left",
                                ["clickCount"] = 1,
                                ["modifiers"] = step.Modifiers
                            });
                            await Task.Delay(200);

                            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                            {
                                ["type"] = "mouseMoved",
                                ["x"] = tgtX,
                                ["y"] = tgtY,
                                ["button"] = "left",
                                ["modifiers"] = step.Modifiers
                            });
                            await Task.Delay(200);

                            await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                            {
                                ["type"] = "mouseReleased",
                                ["x"] = tgtX,
                                ["y"] = tgtY,
                                ["button"] = "left",
                                ["clickCount"] = 1,
                                ["modifiers"] = step.Modifiers
                            });
                            await Task.Delay(300);
                        }
                    }
                }
                else if (step.Type == "scroll")
                {
                    var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = step.Selector };
                    var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
                    int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
                    double scrollX = 400;
                    double scrollY = 300;

                    if (nodeId > 0)
                    {
                        var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                        var model = boxRes["model"] as JsonObject;
                        var content = model?["content"] as JsonArray;
                        if (content != null && content.Count >= 8)
                        {
                            double x1 = content[0]!.GetValue<double>();
                            double y1 = content[1]!.GetValue<double>();
                            double x2 = content[4]!.GetValue<double>();
                            double y2 = content[5]!.GetValue<double>();
                            scrollX = x1 + (x2 - x1) / 2.0;
                            scrollY = y1 + (y2 - y1) / 2.0;
                        }
                    }

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseWheel",
                        ["x"] = scrollX,
                        ["y"] = scrollY,
                        ["deltaX"] = step.OffsetX,
                        ["deltaY"] = step.OffsetY,
                        ["button"] = "none",
                        ["modifiers"] = step.Modifiers
                    });
                    await Task.Delay(200);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error replaying steps: {ex.Message}");
        }
        finally
        {
            if (wasRecording)
            {
                try
                {
                    var mode = _useAutomationProvider?.Invoke() == true ? "automation" : "dom";
                    await _cdpService.SendCommandAsync("Recorder.start", new JsonObject { ["selectorMode"] = mode });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error resuming recording after replay: {ex.Message}");
                }
            }
            IsReplayEnabled = RecordedSteps.Count > 0;
            ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();
        }
    }

    public void ClearRecording()
    {
        RecordedSteps.Clear();
        IsReplayEnabled = false;
        ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();
        UpdateGeneratedCode();
    }

    public void LoadScriptContent(string content)
    {
        RecordedSteps.Clear();
        var parsedSteps = RecordingParser.Parse(content);
        foreach (var step in parsedSteps)
        {
            RecordedSteps.Add(new RecordedStepModel
            {
                Type = step.Type,
                Selector = step.Selector,
                Value = step.Value,
                OffsetX = step.OffsetX,
                OffsetY = step.OffsetY,
                Width = step.Width,
                Height = step.Height,
                Url = step.Url,
                Key = step.Key,
                Button = step.Button,
                ClickCount = step.ClickCount,
                Modifiers = step.Modifiers,
                TargetSelector = step.TargetSelector,
                TargetOffsetX = step.TargetOffsetX,
                TargetOffsetY = step.TargetOffsetY
            });
        }
        IsReplayEnabled = RecordedSteps.Count > 0;
        ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();
        UpdateGeneratedCode();
    }

    public void LoadParsedSteps(List<RecordedStepModel> parsedSteps)
    {
        RecordedSteps.Clear();
        foreach (var step in parsedSteps)
        {
            RecordedSteps.Add(step);
        }
        IsReplayEnabled = RecordedSteps.Count > 0;
        ((RelayCommand)ReplayCommand).RaiseCanExecuteChanged();
        UpdateGeneratedCode();
    }

    public string GetJsonRecording()
    {
        var json = new JsonObject
        {
            ["title"] = $"Recording {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            ["steps"] = new JsonArray(
                RecordedSteps.Select(s => {
                    var stepObj = new JsonObject
                    {
                        ["type"] = s.Type
                    };
                    
                    if (s.Type == "setViewport")
                    {
                        stepObj["width"] = s.Width;
                        stepObj["height"] = s.Height;
                    }
                    else if (s.Type == "navigate")
                    {
                        stepObj["url"] = s.Url;
                    }
                    else if (s.Type == "keydown")
                    {
                        stepObj["key"] = s.Key;
                        if (s.Modifiers > 0) stepObj["modifiers"] = s.Modifiers;
                    }
                    else if (s.Type == "dragAndDrop")
                    {
                        stepObj["selectors"] = new JsonArray { new JsonArray { s.Selector } };
                        stepObj["targetSelectors"] = new JsonArray { new JsonArray { s.TargetSelector } };
                        stepObj["offsetX"] = s.OffsetX;
                        stepObj["offsetY"] = s.OffsetY;
                        stepObj["targetOffsetX"] = s.TargetOffsetX;
                        stepObj["targetOffsetY"] = s.TargetOffsetY;
                        if (s.Modifiers > 0) stepObj["modifiers"] = s.Modifiers;
                    }
                    else if (s.Type == "scroll")
                    {
                        if (!string.IsNullOrEmpty(s.Selector))
                        {
                            stepObj["selectors"] = new JsonArray { new JsonArray { s.Selector } };
                        }
                        stepObj["offsetX"] = s.OffsetX;
                        stepObj["offsetY"] = s.OffsetY;
                    }
                    else // click, change
                    {
                        stepObj["selectors"] = new JsonArray { new JsonArray { s.Selector } };
                        if (s.Type == "change")
                        {
                            stepObj["value"] = s.Value;
                        }
                        else if (s.Type == "click")
                        {
                            stepObj["offsetX"] = s.OffsetX;
                            stepObj["offsetY"] = s.OffsetY;
                            stepObj["button"] = s.Button;
                            stepObj["clickCount"] = s.ClickCount;
                            if (s.Modifiers > 0) stepObj["modifiers"] = s.Modifiers;
                        }
                    }
                    return (JsonNode)stepObj;
                }).ToArray()
            )
        };
        return json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RecordedSteps.Clear();
            IsRecording = false;
            IsReplayEnabled = false;
            UpdateGeneratedCode();
        });
    }
}
