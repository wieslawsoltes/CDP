using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class TestStudioViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private ObservableCollection<TestStudioStepModel> _steps = new();
    private string _yamlCode = "";
    private ObservableCollection<string> _logs = new();
    private bool _isExecuting;
    private bool _isPaused;
    private string _selectedElementSelector = "";
    private string _inputSimText = "";
    private int _delayMs = 1000;
    private TestStudioStepModel? _selectedStep;

    private int _currentStepIndex = 0;
    private CancellationTokenSource? _executionCts;
    private string _appId = "";
    private string _description = "";
    private bool _isUpdatingYaml = false;

    public ObservableCollection<TestStudioStepModel> Steps
    {
        get => _steps;
        set => RaiseAndSetIfChanged(ref _steps, value);
    }

    public string YamlCode
    {
        get => _yamlCode;
        set => RaiseAndSetIfChanged(ref _yamlCode, value);
    }

    public ObservableCollection<string> Logs => _logs;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isExecuting, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isPaused, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public string SelectedElementSelector
    {
        get => _selectedElementSelector;
        set => RaiseAndSetIfChanged(ref _selectedElementSelector, value);
    }

    public string InputSimText
    {
        get => _inputSimText;
        set => RaiseAndSetIfChanged(ref _inputSimText, value);
    }

    public int DelayMs
    {
        get => _delayMs;
        set => RaiseAndSetIfChanged(ref _delayMs, value);
    }

    public TestStudioStepModel? SelectedStep
    {
        get => _selectedStep;
        set => RaiseAndSetIfChanged(ref _selectedStep, value);
    }

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StepOverCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddTapCommand { get; }
    public ICommand AddDoubleTapCommand { get; }
    public ICommand AddLongPressCommand { get; }
    public ICommand AddInputCommand { get; }
    public ICommand AddAssertVisibleCommand { get; }
    public ICommand AddAssertNotVisibleCommand { get; }
    public ICommand AddClearTextCommand { get; }
    public ICommand AddPasteTextCommand { get; }
    public ICommand AddEraseTextCommand { get; }
    public ICommand AddSwipeCommand { get; }
    public ICommand AddDelayCommand { get; }
    public ICommand AddLaunchAppCommand { get; }
    public ICommand AddStopAppCommand { get; }
    public ICommand AddKillAppCommand { get; }
    public ICommand AddClearStateCommand { get; }
    public ICommand AddSetOrientationCommand { get; }
    public ICommand AddSetLocationCommand { get; }
    public ICommand AddTakeScreenshotCommand { get; }
    public ICommand AddAssertTrueCommand { get; }
    public ICommand AddRepeatCommand { get; }
    public ICommand AddRetryCommand { get; }
    public ICommand AddRunFlowCommand { get; }
    public ICommand AddEvalScriptCommand { get; }
    public ICommand AddBackCommand { get; }
    public ICommand AddScrollCommand { get; }
    public ICommand AddOpenLinkCommand { get; }
    public ICommand AddCopyTextFromCommand { get; }
    public ICommand DeleteStepCommand { get; }
    public ICommand MoveUpStepCommand { get; }
    public ICommand MoveDownStepCommand { get; }
    public ICommand ApplyYamlCommand { get; }

    public TestStudioViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        Steps.CollectionChanged += OnStepsCollectionChanged;

        PlayCommand = new RelayCommand(async () => await PlayAsync(), () => _cdpService.IsConnected && Steps.Count > 0 && (!IsExecuting || IsPaused));
        PauseCommand = new RelayCommand(Pause, () => IsExecuting && !IsPaused);
        StopCommand = new RelayCommand(Stop, () => IsExecuting);
        StepOverCommand = new RelayCommand(async () => await StepOverAsync(), () => _cdpService.IsConnected && Steps.Count > 0 && (!IsExecuting || IsPaused));
        ClearCommand = new RelayCommand(ClearAll);

        AddTapCommand = new RelayCommand(async () => await AddTapAsync());
        AddDoubleTapCommand = new RelayCommand(async () => await AddDoubleTapAsync());
        AddLongPressCommand = new RelayCommand(async () => await AddLongPressAsync());
        AddInputCommand = new RelayCommand(async () => await AddInputAsync());
        AddAssertVisibleCommand = new RelayCommand(async () => await AddAssertVisibleAsync());
        AddAssertNotVisibleCommand = new RelayCommand(async () => await AddAssertNotVisibleAsync());
        AddClearTextCommand = new RelayCommand(async () => await AddClearTextAsync());
        AddPasteTextCommand = new RelayCommand(async () => await AddPasteTextAsync());
        AddEraseTextCommand = new RelayCommand(async () => await AddEraseTextAsync());
        AddSwipeCommand = new RelayCommand(async () => await AddSwipeAsync());
        AddDelayCommand = new RelayCommand(AddDelay);
        AddLaunchAppCommand = new RelayCommand(AddLaunchApp);
        AddStopAppCommand = new RelayCommand(AddStopApp);
        AddKillAppCommand = new RelayCommand(AddKillApp);
        AddClearStateCommand = new RelayCommand(async () => await AddClearStateAsync());
        AddSetOrientationCommand = new RelayCommand(async () => await AddSetOrientationAsync());
        AddSetLocationCommand = new RelayCommand(async () => await AddSetLocationAsync());
        AddTakeScreenshotCommand = new RelayCommand(async () => await AddTakeScreenshotAsync());
        AddAssertTrueCommand = new RelayCommand(async () => await AddAssertTrueAsync());
        AddRepeatCommand = new RelayCommand(AddRepeat);
        AddRetryCommand = new RelayCommand(AddRetry);
        AddRunFlowCommand = new RelayCommand(AddRunFlow);
        AddEvalScriptCommand = new RelayCommand(async () => await AddEvalScriptAsync());
        AddBackCommand = new RelayCommand(async () => await AddBackAsync());
        AddScrollCommand = new RelayCommand(async () => await AddScrollAsync());
        AddOpenLinkCommand = new RelayCommand(async () => await AddOpenLinkAsync());
        AddCopyTextFromCommand = new RelayCommand(async () => await AddCopyTextFromAsync());

        DeleteStepCommand = new RelayCommand<TestStudioStepModel>(DeleteStep);
        MoveUpStepCommand = new RelayCommand<TestStudioStepModel>(MoveStepUp);
        MoveDownStepCommand = new RelayCommand<TestStudioStepModel>(MoveStepDown);

        ApplyYamlCommand = new RelayCommand(ApplyYaml, () => !IsExecuting);
    }

    private void CdpService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected && IsExecuting)
            {
                Stop();
            }
            RaiseCommandCanExecuteChanged();
        }
    }

    private void RaiseCommandCanExecuteChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ((RelayCommand)PlayCommand).RaiseCanExecuteChanged();
            ((RelayCommand)PauseCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StepOverCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApplyYamlCommand).RaiseCanExecuteChanged();
        });
    }

    private void OnStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TestStudioStepModel oldStep in e.OldItems)
            {
                oldStep.PropertyChanged -= OnStepPropertyChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (TestStudioStepModel newStep in e.NewItems)
            {
                newStep.PropertyChanged += OnStepPropertyChanged;
            }
        }
        UpdateYaml();
        RaiseCommandCanExecuteChanged();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioStepModel.Action) ||
            e.PropertyName == nameof(TestStudioStepModel.Selector) ||
            e.PropertyName == nameof(TestStudioStepModel.Value))
        {
            UpdateYaml();
        }
    }

    private void UpdateYaml()
    {
        if (_isUpdatingYaml) return;
        _isUpdatingYaml = true;
        try
        {
            YamlCode = TestStudioYamlParser.Generate(Steps.ToList(), _appId, _description);
        }
        finally
        {
            _isUpdatingYaml = false;
        }
    }

    public void ApplyYaml()
    {
        if (IsExecuting) return;

        try
        {
            var parsed = TestStudioYamlParser.Parse(YamlCode, out var appId, out var desc);
            _appId = appId;
            _description = desc;

            _isUpdatingYaml = true;
            try
            {
                // Unsubscribe from old steps
                foreach (var step in Steps)
                {
                    step.PropertyChanged -= OnStepPropertyChanged;
                }

                Steps.Clear();
                foreach (var step in parsed)
                {
                    step.PropertyChanged += OnStepPropertyChanged;
                    Steps.Add(step);
                }
            }
            finally
            {
                _isUpdatingYaml = false;
            }

            Log($"Successfully imported {Steps.Count} steps from YAML.");
        }
        catch (Exception ex)
        {
            Log($"Error parsing YAML: {ex.Message}");
        }
    }

    public async Task PlayAsync()
    {
        if (IsExecuting && !IsPaused) return;

        if (!IsPaused)
        {
            foreach (var step in Steps)
            {
                step.Status = StepStatus.Pending;
                step.ErrorMessage = null;
                step.IsCurrent = false;
            }
            _currentStepIndex = 0;
        }

        IsExecuting = true;
        IsPaused = false;

        _executionCts = new CancellationTokenSource();
        var token = _executionCts.Token;

        try
        {
            await RunLoopAsync(token);
        }
        catch (OperationCanceledException)
        {
            Log("Execution paused or stopped.");
        }
        catch (Exception ex)
        {
            Log($"Execution error: {ex.Message}");
        }
        finally
        {
            if (!IsPaused)
            {
                IsExecuting = false;
            }
            RaiseCommandCanExecuteChanged();
        }
    }

    public void Pause()
    {
        if (!IsExecuting || IsPaused) return;
        IsPaused = true;
        _executionCts?.Cancel();
        RaiseCommandCanExecuteChanged();
    }

    public void Stop()
    {
        IsExecuting = false;
        IsPaused = false;
        _executionCts?.Cancel();
        _currentStepIndex = 0;
        foreach (var step in Steps)
        {
            step.Status = StepStatus.Pending;
            step.ErrorMessage = null;
            step.IsCurrent = false;
        }
        Log("Execution stopped.");
        RaiseCommandCanExecuteChanged();
    }

    public async Task StepOverAsync()
    {
        if (IsExecuting && !IsPaused) return;

        if (!IsExecuting)
        {
            foreach (var step in Steps)
            {
                step.Status = StepStatus.Pending;
                step.ErrorMessage = null;
                step.IsCurrent = false;
            }
            _currentStepIndex = 0;
            IsExecuting = true;
        }

        IsPaused = true;

        if (_currentStepIndex >= Steps.Count)
        {
            IsExecuting = false;
            IsPaused = false;
            Log("No more steps to execute.");
            return;
        }

        var stepToExecute = Steps[_currentStepIndex];
        try
        {
            stepToExecute.IsCurrent = true;
            stepToExecute.Status = StepStatus.Running;
            Log($"Running step {_currentStepIndex + 1}: {stepToExecute.ActionDisplay}...");

            await ExecuteSingleStepAsync(stepToExecute, CancellationToken.None);

            stepToExecute.Status = StepStatus.Passed;
            Log($"Step {_currentStepIndex + 1} passed.");
            _currentStepIndex++;
        }
        catch (Exception ex)
        {
            stepToExecute.Status = StepStatus.Failed;
            stepToExecute.ErrorMessage = ex.Message;
            Log($"Step {_currentStepIndex + 1} failed: {ex.Message}");
            _currentStepIndex++;
        }
        finally
        {
            stepToExecute.IsCurrent = false;
            if (_currentStepIndex >= Steps.Count)
            {
                IsExecuting = false;
                IsPaused = false;
                Log("Execution finished.");
            }
            RaiseCommandCanExecuteChanged();
        }
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (_currentStepIndex < Steps.Count)
        {
            token.ThrowIfCancellationRequested();

            var step = Steps[_currentStepIndex];
            step.IsCurrent = true;
            step.Status = StepStatus.Running;
            Log($"Running step {_currentStepIndex + 1}: {step.ActionDisplay}...");

            try
            {
                await ExecuteSingleStepAsync(step, token);
                step.Status = StepStatus.Passed;
                Log($"Step {_currentStepIndex + 1} passed.");
                _currentStepIndex++;
            }
            catch (OperationCanceledException)
            {
                step.Status = StepStatus.Pending;
                Log($"Step {_currentStepIndex + 1} paused.");
                throw;
            }
            catch (Exception ex)
            {
                step.Status = StepStatus.Failed;
                step.ErrorMessage = ex.Message;
                Log($"Step {_currentStepIndex + 1} failed: {ex.Message}");
                throw;
            }
            finally
            {
                step.IsCurrent = false;
            }
        }

        Log("Execution finished successfully.");
        IsExecuting = false;
        IsPaused = false;
    }

    private async Task ExecuteSingleStepAsync(TestStudioStepModel step, CancellationToken token)
    {
        var action = step.Action;
        if (string.IsNullOrEmpty(action)) return;

        switch (action)
        {
            case "launchApp":
                {
                    string targetUrl = _cdpService.ConnectedHost;
                    if (string.IsNullOrEmpty(targetUrl))
                    {
                        targetUrl = "http://localhost:9222/";
                    }
                    Log($"Launching app / navigating to: {targetUrl}");
                    await _cdpService.SendCommandAsync("Page.navigate", new JsonObject { ["url"] = targetUrl });
                    await Task.Delay(500, token);
                    break;
                }
            case "tapOn":
                {
                    var (x, y, nodeId) = await ResolveCoordinatesAsync(step, token);
                    if (nodeId > 0)
                    {
                        try
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    Log($"Tapping at coordinate ({x:F1}, {y:F1})");
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mousePressed",
                        ["x"] = x,
                        ["y"] = y,
                        ["button"] = "left",
                        ["clickCount"] = 1,
                        ["modifiers"] = 0
                    });
                    await Task.Delay(50, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseReleased",
                        ["x"] = x,
                        ["y"] = y,
                        ["button"] = "left",
                        ["clickCount"] = 1,
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "doubleTapOn":
                {
                    var (x, y, nodeId) = await ResolveCoordinatesAsync(step, token);
                    if (nodeId > 0)
                    {
                        try
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    Log($"Double tapping at coordinate ({x:F1}, {y:F1})");
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(50, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(100, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 2, ["modifiers"] = 0 });
                    await Task.Delay(50, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 2, ["modifiers"] = 0 });
                    await Task.Delay(200, token);
                    break;
                }
            case "longPressOn":
                {
                    var (x, y, nodeId) = await ResolveCoordinatesAsync(step, token);
                    if (nodeId > 0)
                    {
                        try
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    Log($"Long pressing at coordinate ({x:F1}, {y:F1})");
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(1000, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(200, token);
                    break;
                }

            case "inputText":
                {
                    if (!string.IsNullOrEmpty(step.Selector))
                    {
                        int retryCount = 0;
                        bool success = false;
                        while (retryCount < 3 && !success)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                Log($"Waiting for element '{step.Selector}' to be visible...");
                                int nodeId = await WaitForElementVisibleAsync(step.Selector, token);

                                Log($"Focusing element and typing '{step.Value}'");
                                await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                                await Task.Delay(100, token);
                                success = true;
                            }
                            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) && retryCount < 2)
                            {
                                Log($"Focus failed due to Node ID invalidation: {ex.Message}. Retrying...");
                                retryCount++;
                                await Task.Delay(100, token);
                            }
                        }
                    }
                    else
                    {
                        Log($"Typing '{step.Value}' on currently focused control...");
                    }
                    await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = step.Value ?? "" });
                    await Task.Delay(200, token);
                    break;
                }
            case "clearText":
                {
                    if (!string.IsNullOrEmpty(step.Selector))
                    {
                        int retryCount = 0;
                        bool success = false;
                        while (retryCount < 3 && !success)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                Log($"Waiting for element '{step.Selector}' to be visible...");
                                int nodeId = await WaitForElementVisibleAsync(step.Selector, token);

                                Log("Focusing element and clearing text...");
                                await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                                await Task.Delay(100, token);
                                success = true;
                            }
                            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) && retryCount < 2)
                            {
                                Log($"Focus failed due to Node ID invalidation: {ex.Message}. Retrying...");
                                retryCount++;
                                await Task.Delay(100, token);
                            }
                        }
                    }
                    else
                    {
                        Log("Clearing text on currently focused control...");
                    }

                    // Ctrl+A
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = "a",
                        ["modifiers"] = 2
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "a",
                        ["modifiers"] = 2
                    });

                    // Cmd+A
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = "a",
                        ["modifiers"] = 4
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "a",
                        ["modifiers"] = 4
                    });

                    await Task.Delay(50, token);

                    // Backspace
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = "Backspace"
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "Backspace"
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "assertVisible":
                {
                    if (string.IsNullOrEmpty(step.Selector))
                    {
                        throw new Exception("assertVisible step requires a Selector.");
                    }
                    Log($"Asserting visibility of element '{step.Selector}'...");
                    await WaitForElementVisibleAsync(step.Selector, token);
                    Log("Assertion passed: Element is visible.");
                    break;
                }
            case "assertNotVisible":
                {
                    if (string.IsNullOrEmpty(step.Selector))
                    {
                        throw new Exception("assertNotVisible step requires a Selector.");
                    }
                    Log($"Asserting element '{step.Selector}' is NOT visible...");
                    await WaitForElementNotVisibleAsync(step.Selector, token);
                    Log("Assertion passed: Element is not visible.");
                    break;
                }
            case "delay":
                {
                    int dMs = 1000;
                    if (int.TryParse(step.Value, out int parsedVal))
                    {
                        dMs = parsedVal;
                    }
                    Log($"Delaying for {dMs} ms...");
                    await Task.Delay(dMs, token);
                    break;
                }
            case "openLink":
                {
                    if (string.IsNullOrEmpty(step.Value)) throw new Exception("openLink step requires a URL value.");
                    Log($"Opening link: '{step.Value}'");
                    await _cdpService.SendCommandAsync("Page.navigate", new JsonObject { ["url"] = step.Value });
                    await Task.Delay(1000, token);
                    break;
                }
            case "copyTextFrom":
                {
                    if (string.IsNullOrEmpty(step.Selector)) throw new Exception("copyTextFrom step requires a Selector.");
                    Log($"Copying text from element '{step.Selector}'...");
                    int nodeId = await WaitForElementVisibleAsync(step.Selector, token);
                    var resolveRes = await _cdpService.SendCommandAsync("DOM.resolveNode", new JsonObject { ["nodeId"] = nodeId });
                    var objectNode = resolveRes["object"] as JsonObject;
                    string objectId = objectNode?["objectId"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(objectId)) throw new Exception($"Could not resolve remote object for element '{step.Selector}'.");

                    var callParams = new JsonObject
                    {
                        ["objectId"] = objectId,
                        ["functionDeclaration"] = "function() { return this.Content || this.Text || this.value || this.textContent || this.innerText || ''; }",
                        ["returnByValue"] = true
                    };
                    var callRes = await _cdpService.SendCommandAsync("Runtime.callFunctionOn", callParams);
                    var resultNode = callRes["result"] as JsonObject;
                    string text = resultNode?["value"]?.GetValue<string>() ?? "";

                    Log($"Copied text: '{text}'");

                    Avalonia.Input.Platform.IClipboard? clipboard = null;
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        clipboard = desktop.MainWindow?.Clipboard ?? desktop.Windows.FirstOrDefault(w => w.Clipboard != null)?.Clipboard;
                    }
                    else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
                    {
                        clipboard = Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
                    }
                    if (clipboard != null)
                    {
                        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
                    }
                    break;
                }
            case "back":
                {
                    Log("Navigating back in history...");
                    var historyRes = await _cdpService.SendCommandAsync("Page.getNavigationHistory");
                    int currentIndex = historyRes["currentIndex"]?.GetValue<int>() ?? -1;
                    var entries = historyRes["entries"] as JsonArray;
                    if (entries != null && currentIndex > 0 && currentIndex < entries.Count)
                    {
                        var prevEntry = entries[currentIndex - 1] as JsonObject;
                        int entryId = prevEntry?["id"]?.GetValue<int>() ?? 0;
                        if (entryId > 0)
                        {
                            await _cdpService.SendCommandAsync("Page.navigateToHistoryEntry", new JsonObject { ["entryId"] = entryId });
                            await Task.Delay(500, token);
                        }
                        else
                        {
                            throw new Exception("Could not find previous history entry ID.");
                        }
                    }
                    else
                    {
                        throw new Exception("No back history available.");
                    }
                    break;
                }
            case "dragAndDrop":
                {
                    if (string.IsNullOrEmpty(step.Selector))
                    {
                        throw new Exception("dragAndDrop step requires a Selector.");
                    }

                    string targetSelector = "";
                    double offsetX = 0;
                    double offsetY = 0;
                    double targetOffsetX = 0;
                    double targetOffsetY = 0;

                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        var props = ParseKeyValuePairs(step.Value);
                        if (props.TryGetValue("targetselector", out var ts)) targetSelector = ts;
                        else if (props.TryGetValue("targetSelector", out var ts2)) targetSelector = ts2;

                        if (props.TryGetValue("offsetx", out var ox) && double.TryParse(ox, out double oxVal)) offsetX = oxVal;
                        else if (props.TryGetValue("offsetX", out var ox2) && double.TryParse(ox2, out double oxVal2)) offsetX = oxVal2;

                        if (props.TryGetValue("offsety", out var oy) && double.TryParse(oy, out double oyVal)) offsetY = oyVal;
                        else if (props.TryGetValue("offsetY", out var oy2) && double.TryParse(oy2, out double oyVal2)) offsetY = oyVal2;

                        if (props.TryGetValue("targetoffsetx", out var tox) && double.TryParse(tox, out double toxVal)) targetOffsetX = toxVal;
                        else if (props.TryGetValue("targetOffsetX", out var tox2) && double.TryParse(tox2, out double toxVal2)) targetOffsetX = toxVal2;

                        if (props.TryGetValue("targetoffsety", out var toy) && double.TryParse(toy, out double toyVal)) targetOffsetY = toyVal;
                        else if (props.TryGetValue("targetOffsetY", out var toy2) && double.TryParse(toy2, out double toyVal2)) targetOffsetY = toyVal2;
                    }

                    if (string.IsNullOrEmpty(targetSelector))
                    {
                        throw new Exception("dragAndDrop step requires a targetSelector.");
                    }

                    Log($"Waiting for drag source element '{step.Selector}' to be visible...");
                    var sourceNodeId = await WaitForElementVisibleAsync(step.Selector, token);

                    Log($"Waiting for drag target element '{targetSelector}' to be visible...");
                    var targetNodeId = await WaitForElementVisibleAsync(targetSelector, token);

                    var srcBoxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = sourceNodeId });
                    var srcModel = srcBoxRes["model"] as JsonObject;
                    var srcContent = srcModel?["content"] as JsonArray;
                    var srcBorder = srcModel?["border"] as JsonArray ?? srcContent;

                    var tgtBoxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = targetNodeId });
                    var tgtModel = tgtBoxRes["model"] as JsonObject;
                    var tgtContent = tgtModel?["content"] as JsonArray;
                    var tgtBorder = tgtModel?["border"] as JsonArray ?? tgtContent;

                    if (srcContent == null || srcContent.Count < 8 || tgtContent == null || tgtContent.Count < 8)
                    {
                        throw new Exception("Failed to retrieve box model content for source or target element.");
                    }

                    double srcX = (offsetX != 0.0 || offsetY != 0.0) 
                        ? srcBorder[0]!.GetValue<double>() + offsetX 
                        : srcContent[0]!.GetValue<double>() + (srcContent[4]!.GetValue<double>() - srcContent[0]!.GetValue<double>()) / 2.0;
                    double srcY = (offsetX != 0.0 || offsetY != 0.0) 
                        ? srcBorder[1]!.GetValue<double>() + offsetY 
                        : srcContent[1]!.GetValue<double>() + (srcContent[5]!.GetValue<double>() - srcContent[1]!.GetValue<double>()) / 2.0;

                    double tgtX = (targetOffsetX != 0.0 || targetOffsetY != 0.0) 
                        ? tgtBorder[0]!.GetValue<double>() + targetOffsetX 
                        : tgtContent[0]!.GetValue<double>() + (tgtContent[4]!.GetValue<double>() - tgtContent[0]!.GetValue<double>()) / 2.0;
                    double tgtY = (targetOffsetX != 0.0 || targetOffsetY != 0.0) 
                        ? tgtBorder[1]!.GetValue<double>() + targetOffsetY 
                        : tgtContent[1]!.GetValue<double>() + (tgtContent[5]!.GetValue<double>() - tgtContent[1]!.GetValue<double>()) / 2.0;

                    Log($"Dragging from ({srcX:F1}, {srcY:F1}) to ({tgtX:F1}, {tgtY:F1})");

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseMoved",
                        ["x"] = srcX,
                        ["y"] = srcY,
                        ["button"] = "none",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(100, token);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mousePressed",
                        ["x"] = srcX,
                        ["y"] = srcY,
                        ["button"] = "left",
                        ["clickCount"] = 1,
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseMoved",
                        ["x"] = tgtX,
                        ["y"] = tgtY,
                        ["button"] = "left",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseReleased",
                        ["x"] = tgtX,
                        ["y"] = tgtY,
                        ["button"] = "left",
                        ["clickCount"] = 1,
                        ["modifiers"] = 0
                    });
                    await Task.Delay(300, token);
                    break;
                }
            case "scroll":
                {
                    double scrollX = 400;
                    double scrollY = 300;

                    if (!string.IsNullOrEmpty(step.Selector))
                    {
                        Log($"Waiting for scroll target element '{step.Selector}' to be visible...");
                        var nodeId = await WaitForElementVisibleAsync(step.Selector, token);
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
                    else
                    {
                        try
                        {
                            var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                            var viewport = metrics["cssVisualViewport"] as JsonObject;
                            if (viewport != null)
                            {
                                double w = viewport["width"]?.GetValue<double>() ?? 800;
                                double h = viewport["height"]?.GetValue<double>() ?? 600;
                                scrollX = w / 2.0;
                                scrollY = h / 2.0;
                            }
                        }
                        catch { }
                    }

                    string direction = "down";
                    double amount = 100;

                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        var props = ParseKeyValuePairs(step.Value);
                        if (props.TryGetValue("direction", out var dir))
                        {
                            direction = dir;
                        }
                        if (props.TryGetValue("amount", out var amt) && double.TryParse(amt, out double a))
                        {
                            amount = a;
                        }
                        else if (double.TryParse(step.Value, out double singleAmt))
                        {
                            amount = singleAmt;
                        }
                    }

                    double deltaX = 0;
                    double deltaY = 0;
                    if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = -amount;
                    }
                    else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = amount;
                    }
                    else if (direction.Equals("left", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = amount;
                    }
                    else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = -amount;
                    }

                    Log($"Scrolling at ({scrollX:F1}, {scrollY:F1}) with deltaX={deltaX}, deltaY={deltaY}");
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseWheel",
                        ["x"] = scrollX,
                        ["y"] = scrollY,
                        ["deltaX"] = deltaX,
                        ["deltaY"] = deltaY,
                        ["button"] = "none",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "pressKey":
                {
                    if (string.IsNullOrEmpty(step.Value))
                    {
                        throw new Exception("pressKey step requires a Value (key).");
                    }
                    Log($"Pressing key '{step.Value}'");
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = step.Value,
                        ["code"] = step.Value,
                        ["text"] = "",
                        ["modifiers"] = 0
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = step.Value,
                        ["code"] = step.Value,
                        ["text"] = "",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "pasteText":
                {
                    string clipboardText = step.Value ?? "";
                    if (string.IsNullOrEmpty(clipboardText))
                    {
                        try
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
                                clipboardText = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) ?? "";
                            }
                        }
                        catch { }
                    }
                    Log($"Pasting text: '{clipboardText}'");
                    await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = clipboardText });
                    await Task.Delay(200, token);
                    break;
                }
            case "eraseText":
                {
                    int count = 1;
                    if (int.TryParse(step.Value, out int parsed)) count = parsed;
                    Log($"Erasing {count} characters...");
                    for (int i = 0; i < count; i++)
                    {
                        await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "rawKeyDown", ["key"] = "Backspace", ["code"] = "Backspace", ["windowsVirtualKeyCode"] = 8 });
                        await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["key"] = "Backspace", ["code"] = "Backspace", ["windowsVirtualKeyCode"] = 8 });
                        await Task.Delay(50, token);
                    }
                    await Task.Delay(150, token);
                    break;
                }
            case "swipe":
                {
                    double startX = 400, startY = 300, endX = 200, endY = 300;
                    double width = 800, height = 600;
                    try
                    {
                        var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                        var viewport = metrics["cssVisualViewport"] as JsonObject;
                        if (viewport != null)
                        {
                            width = viewport["width"]?.GetValue<double>() ?? width;
                            height = viewport["height"]?.GetValue<double>() ?? height;
                        }
                    }
                    catch { }

                    string direction = "left";
                    var props = ParseKeyValuePairs(step.Value);
                    if (props.TryGetValue("direction", out var sDir))
                    {
                        direction = sDir;
                    }

                    if (props.TryGetValue("start", out var startStr))
                    {
                        var sc = ParseCoordinates(startStr);
                        if (sc.HasValue) { startX = sc.Value.x; startY = sc.Value.y; }
                    }
                    else
                    {
                        if (direction.Equals("left", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.8; startY = height * 0.5; }
                        else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.2; startY = height * 0.5; }
                        else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.5; startY = height * 0.8; }
                        else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.5; startY = height * 0.2; }
                    }

                    if (props.TryGetValue("end", out var endStr))
                    {
                        var ec = ParseCoordinates(endStr);
                        if (ec.HasValue) { endX = ec.Value.x; endY = ec.Value.y; }
                    }
                    else
                    {
                        if (direction.Equals("left", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.2; endY = height * 0.5; }
                        else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.8; endY = height * 0.5; }
                        else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.5; endY = height * 0.2; }
                        else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.5; endY = height * 0.8; }
                    }

                    Log($"Swiping from ({startX:F1}, {startY:F1}) to ({endX:F1}, {endY:F1})");
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = startX, ["y"] = startY, ["button"] = "left", ["clickCount"] = 1 });
                    int stepsCount = 10;
                    for (int i = 1; i <= stepsCount; i++)
                    {
                        double t = (double)i / stepsCount;
                        double curX = startX + (endX - startX) * t;
                        double curY = startY + (endY - startY) * t;
                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseMoved", ["x"] = curX, ["y"] = curY, ["button"] = "left" });
                        await Task.Delay(20, token);
                    }
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = endX, ["y"] = endY, ["button"] = "left", ["clickCount"] = 1 });
                    await Task.Delay(200, token);
                    break;
                }
            case "stopApp":
            case "killApp":
                {
                    Log($"Closing target application target connection...");
                    try
                    {
                        await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = "Avalonia.Application.Current?.Shutdown()" });
                    }
                    catch { }
                    await Task.Delay(200, token);
                    break;
                }
            case "clearState":
                {
                    Log("Reloading target application page/view to reset state...");
                    await _cdpService.SendCommandAsync("Page.reload", new JsonObject());
                    await Task.Delay(500, token);
                    break;
                }
            case "setOrientation":
                {
                    string orientation = step.Value?.Trim().ToLower() ?? "portrait";
                    Log($"Setting device metrics override to orientation: {orientation}");
                    bool isLandscape = orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase);
                    int w = isLandscape ? 1280 : 800;
                    int h = isLandscape ? 800 : 1280;
                    try
                    {
                        await _cdpService.SendCommandAsync("Emulation.setDeviceMetricsOverride", new JsonObject
                        {
                            ["width"] = w,
                            ["height"] = h,
                            ["deviceScaleFactor"] = 1.0,
                            ["mobile"] = true,
                            ["screenOrientation"] = new JsonObject { ["type"] = isLandscape ? "landscapePrimary" : "portraitPrimary", ["angle"] = isLandscape ? 90 : 0 }
                        });
                    }
                    catch { }
                    await Task.Delay(200, token);
                    break;
                }
            case "setLocation":
                {
                    double lat = 37.7749;
                    double lon = -122.4194;
                    var props = ParseKeyValuePairs(step.Value);
                    if (props.TryGetValue("latitude", out var latStr)) double.TryParse(latStr, out lat);
                    if (props.TryGetValue("longitude", out var lonStr)) double.TryParse(lonStr, out lon);
                    Log($"Setting geolocation override mock: Lat={lat}, Lon={lon}");
                    try
                    {
                        await _cdpService.SendCommandAsync("Emulation.setGeolocationOverride", new JsonObject
                        {
                            ["latitude"] = lat,
                            ["longitude"] = lon,
                            ["accuracy"] = 100
                        });
                    }
                    catch { }
                    await Task.Delay(200, token);
                    break;
                }
            case "takeScreenshot":
                {
                    string filename = step.Value?.Trim() ?? "";
                    if (string.IsNullOrEmpty(filename)) filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    Log($"Capturing page screenshot as {filename}...");
                    try
                    {
                        var res = await _cdpService.SendCommandAsync("Page.captureScreenshot", new JsonObject());
                        string base64Data = res["data"]?.GetValue<string>() ?? "";
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            byte[] bytes = Convert.FromBase64String(base64Data);
                            await System.IO.File.WriteAllBytesAsync(filename, bytes);
                            Log($"Screenshot written to file: {System.IO.Path.GetFullPath(filename)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to take screenshot: {ex.Message}");
                    }
                    break;
                }
            case "assertTrue":
                {
                    if (string.IsNullOrEmpty(step.Value)) throw new Exception("assertTrue requires a value containing the expression.");
                    Log($"Asserting expression evaluates to true: {step.Value}");
                    var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = step.Value });
                    var resultNode = evalRes["result"] as JsonObject;
                    var val = resultNode?["value"]?.GetValue<object>()?.ToString();
                    if (val != "True" && val != "true" && val != "1")
                    {
                        throw new Exception($"Assertion failed: Expression '{step.Value}' evaluated to '{val ?? "null"}' (not true).");
                    }
                    Log("Assertion check passed successfully.");
                    break;
                }
            case "evalScript":
            case "runScript":
                {
                    if (string.IsNullOrEmpty(step.Value)) throw new Exception("evalScript requires a script value.");
                    Log($"Evaluating script on target: {step.Value}");
                    var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = step.Value });
                    var resultNode = evalRes["result"] as JsonObject;
                    Log($"Script execution result: {resultNode?["value"]?.ToString() ?? "void"}");
                    break;
                }
            case "repeat":
            case "retry":
                {
                    Log($"Executing loop/retry command '{step.Action}' with iterations count={step.Value ?? "1"}");
                    break;
                }
            case "runFlow":
                {
                    string flowPath = step.Value?.Trim() ?? "";
                    if (string.IsNullOrEmpty(flowPath)) throw new Exception("runFlow requires a path to a YAML flow file.");
                    Log($"Running nested flow: {flowPath}...");
                    if (!System.IO.File.Exists(flowPath)) throw new Exception($"Flow file not found: {flowPath}");
                    string subYaml = await System.IO.File.ReadAllTextAsync(flowPath);
                    var subSteps = TestStudioYamlParser.Parse(subYaml, out _, out _);
                    Log($"Executing {subSteps.Count} steps recursively from nested flow: {flowPath}");
                    foreach (var subStep in subSteps)
                    {
                        await ExecuteSingleStepAsync(subStep, token);
                    }
                    Log($"Completed nested flow: {flowPath}");
                    break;
                }
            case "scrollUntilVisible":
                {
                    if (string.IsNullOrEmpty(step.Selector))
                    {
                        throw new Exception("scrollUntilVisible step requires a Selector.");
                    }

                    string direction = "down";
                    int maxScrolls = 10;
                    double amount = 150;

                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        var props = ParseKeyValuePairs(step.Value);
                        if (props.TryGetValue("direction", out var dir))
                        {
                            direction = dir;
                        }
                        if (props.TryGetValue("maxscrolls", out var msStr) && int.TryParse(msStr, out int ms))
                        {
                            maxScrolls = ms;
                        }
                    }

                    double scrollX = 400;
                    double scrollY = 300;
                    try
                    {
                        var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                        var viewport = metrics["cssVisualViewport"] as JsonObject;
                        if (viewport != null)
                        {
                            double w = viewport["width"]?.GetValue<double>() ?? 800;
                            double h = viewport["height"]?.GetValue<double>() ?? 600;
                            scrollX = w / 2.0;
                            scrollY = h / 2.0;
                        }
                    }
                    catch { }

                    double deltaX = 0;
                    double deltaY = 0;
                    if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = -amount;
                    }
                    else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = amount;
                    }
                    else if (direction.Equals("left", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = amount;
                    }
                    else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = -amount;
                    }

                    int scrollCount = 0;
                    bool visible = false;
                    while (scrollCount <= maxScrolls)
                    {
                        token.ThrowIfCancellationRequested();

                        var nodeId = await CheckElementVisibleAsync(step.Selector);
                        if (nodeId.HasValue)
                        {
                            visible = true;
                            Log($"Element '{step.Selector}' is visible after {scrollCount} scrolls.");
                            break;
                        }

                        if (scrollCount == maxScrolls)
                        {
                            break;
                        }

                        Log($"Element '{step.Selector}' not visible. Scrolling ({scrollCount + 1}/{maxScrolls}) at ({scrollX:F1}, {scrollY:F1}) with deltaX={deltaX}, deltaY={deltaY}...");
                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                        {
                            ["type"] = "mouseWheel",
                            ["x"] = scrollX,
                            ["y"] = scrollY,
                            ["deltaX"] = deltaX,
                            ["deltaY"] = deltaY,
                            ["button"] = "none",
                            ["modifiers"] = 0
                        });

                        scrollCount++;
                        await Task.Delay(300, token);
                    }

                    if (!visible)
                    {
                        throw new Exception($"Element '{step.Selector}' did not become visible after {maxScrolls} scrolls.");
                    }
                    break;
                }
            default:
                throw new NotSupportedException($"Step action '{action}' is not supported.");
        }
    }

    private async Task<int> WaitForElementVisibleAsync(string selector, CancellationToken token)
    {
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalSeconds < 5.0)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
                var root = docRes["root"] as JsonObject;
                int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

                var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = selector };
                var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
                int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
                if (nodeId > 0)
                {
                    var (w, h) = await GetElementSizeAsync(nodeId);
                    if (w > 0 && h > 0)
                    {
                        return nodeId;
                    }
                }
            }
            catch
            {
                // Ignore and retry
            }
            await Task.Delay(200, token);
        }
        throw new TimeoutException($"Element with selector '{selector}' was not visible within 5 seconds.");
    }

    private async Task WaitForElementNotVisibleAsync(string selector, CancellationToken token)
    {
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalSeconds < 5.0)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
                var root = docRes["root"] as JsonObject;
                int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

                var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = selector };
                var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
                int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
                if (nodeId == 0)
                {
                    return;
                }
                var (w, h) = await GetElementSizeAsync(nodeId);
                if (w <= 0 || h <= 0)
                {
                    return;
                }
            }
            catch
            {
                return;
            }
            await Task.Delay(200, token);
        }
        throw new TimeoutException($"Element with selector '{selector}' was still visible after 5 seconds.");
    }

    private async Task<int?> CheckElementVisibleAsync(string selector)
    {
        try
        {
            var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
            var root = docRes["root"] as JsonObject;
            int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

            var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = selector };
            var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
            int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
            if (nodeId > 0)
            {
                var (w, h) = await GetElementSizeAsync(nodeId);
                if (w > 0 && h > 0)
                {
                    return nodeId;
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    private async Task<(double width, double height)> GetElementSizeAsync(int nodeId)
    {
        try
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
                return (x2 - x1, y2 - y1);
            }
        }
        catch
        {
            // Ignore
        }
        return (0, 0);
    }

    private async Task AddInteractiveStepAsync(TestStudioStepModel step)
    {
        // 1. Mark as running & current
        step.Status = StepStatus.Running;
        step.IsCurrent = true;

        try
        {
            // 2. Immediately execute the action in real-time
            await ExecuteSingleStepAsync(step, CancellationToken.None);

            // 3. Mark as passed
            step.Status = StepStatus.Passed;
            Log($"Added and executed step: {step.ActionDisplay} (Passed)");
        }
        catch (Exception ex)
        {
            // 4. If it fails, log error and show it to user
            step.Status = StepStatus.Failed;
            step.ErrorMessage = ex.Message;
            Log($"Failed to execute step {step.ActionDisplay} immediately: {ex.Message}");
        }
        finally
        {
            step.IsCurrent = false;
            // 5. Append to step list
            Steps.Add(step);
        }
    }

    public async Task AddTapAsync()
    {
        var step = new TestStudioStepModel { Action = "tapOn", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddDoubleTapAsync()
    {
        var step = new TestStudioStepModel { Action = "doubleTapOn", Selector = SelectedElementSelector, Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddLongPressAsync()
    {
        var step = new TestStudioStepModel { Action = "longPressOn", Selector = SelectedElementSelector, Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddInputAsync()
    {
        var step = new TestStudioStepModel { Action = "inputText", Selector = SelectedElementSelector, Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertVisibleAsync()
    {
        var step = new TestStudioStepModel { Action = "assertVisible", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertNotVisibleAsync()
    {
        var step = new TestStudioStepModel { Action = "assertNotVisible", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddClearTextAsync()
    {
        var step = new TestStudioStepModel { Action = "clearText", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddPasteTextAsync()
    {
        var step = new TestStudioStepModel { Action = "pasteText", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddEraseTextAsync()
    {
        var step = new TestStudioStepModel { Action = "eraseText", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSwipeAsync()
    {
        var step = new TestStudioStepModel { Action = "swipe", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public void AddDelay()
    {
        Steps.Add(new TestStudioStepModel { Action = "delay", Selector = "", Value = string.IsNullOrEmpty(InputSimText) ? DelayMs.ToString() : InputSimText });
    }

    public void AddLaunchApp()
    {
        Steps.Add(new TestStudioStepModel { Action = "launchApp", Selector = "", Value = InputSimText });
    }

    public void AddStopApp()
    {
        Steps.Add(new TestStudioStepModel { Action = "stopApp", Selector = "", Value = InputSimText });
    }

    public void AddKillApp()
    {
        Steps.Add(new TestStudioStepModel { Action = "killApp", Selector = "", Value = InputSimText });
    }

    public async Task AddClearStateAsync()
    {
        var step = new TestStudioStepModel { Action = "clearState", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSetOrientationAsync()
    {
        var step = new TestStudioStepModel { Action = "setOrientation", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSetLocationAsync()
    {
        var step = new TestStudioStepModel { Action = "setLocation", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddTakeScreenshotAsync()
    {
        var step = new TestStudioStepModel { Action = "takeScreenshot", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddOpenLinkAsync()
    {
        var step = new TestStudioStepModel { Action = "openLink", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddCopyTextFromAsync()
    {
        var step = new TestStudioStepModel { Action = "copyTextFrom", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertTrueAsync()
    {
        var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public void AddRepeat()
    {
        Steps.Add(new TestStudioStepModel { Action = "repeat", Selector = "", Value = InputSimText });
    }

    public void AddRetry()
    {
        Steps.Add(new TestStudioStepModel { Action = "retry", Selector = "", Value = InputSimText });
    }

    public void AddRunFlow()
    {
        Steps.Add(new TestStudioStepModel { Action = "runFlow", Selector = "", Value = InputSimText });
    }

    public async Task AddEvalScriptAsync()
    {
        var step = new TestStudioStepModel { Action = "evalScript", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddBackAsync()
    {
        var step = new TestStudioStepModel { Action = "back", Selector = "", Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddScrollAsync()
    {
        var step = new TestStudioStepModel { Action = "scroll", Selector = SelectedElementSelector, Value = string.IsNullOrEmpty(InputSimText) ? "direction: down, amount: 100" : InputSimText };
        await AddInteractiveStepAsync(step);
    }

    private void DeleteStep(TestStudioStepModel? step)
    {
        if (step != null && Steps.Contains(step))
        {
            Steps.Remove(step);
        }
    }

    private void MoveStepUp(TestStudioStepModel? step)
    {
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        if (idx > 0)
        {
            _isUpdatingYaml = true;
            try
            {
                Steps.RemoveAt(idx);
                Steps.Insert(idx - 1, step);
            }
            finally
            {
                _isUpdatingYaml = false;
            }
            UpdateYaml();
        }
    }

    private void MoveStepDown(TestStudioStepModel? step)
    {
        if (step == null) return;
        int idx = Steps.IndexOf(step);
        if (idx >= 0 && idx < Steps.Count - 1)
        {
            _isUpdatingYaml = true;
            try
            {
                Steps.RemoveAt(idx);
                Steps.Insert(idx + 1, step);
            }
            finally
            {
                _isUpdatingYaml = false;
            }
            UpdateYaml();
        }
    }

    private void ClearAll()
    {
        Steps.Clear();
        Logs.Clear();
        _currentStepIndex = 0;
    }

    private void Log(string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        });
    }

    private async Task<(double x, double y, int nodeId)> ResolveCoordinatesAsync(TestStudioStepModel step, CancellationToken token)
    {
        int retryCount = 0;
        while (retryCount < 3)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                double? targetX = null;
                double? targetY = null;
                int nodeId = 0;

                if (!string.IsNullOrEmpty(step.Selector))
                {
                    Log($"Waiting for element '{step.Selector}' to be visible...");
                    nodeId = await WaitForElementVisibleAsync(step.Selector, token);
                    Log($"Element resolved. Fetching box model...");
                    var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                    var model = boxRes["model"] as JsonObject;
                    var content = model?["content"] as JsonArray;
                    if (content != null && content.Count >= 8)
                    {
                        double x1 = content[0]!.GetValue<double>();
                        double y1 = content[1]!.GetValue<double>();
                        double x2 = content[4]!.GetValue<double>();
                        double y2 = content[5]!.GetValue<double>();
                        targetX = x1 + (x2 - x1) / 2.0;
                        targetY = y1 + (y2 - y1) / 2.0;
                    }
                    else
                    {
                        throw new Exception("Could not retrieve content box from box model.");
                    }
                }
                else if (!string.IsNullOrEmpty(step.Value))
                {
                    var coords = ParseCoordinates(step.Value);
                    if (coords.HasValue)
                    {
                        targetX = coords.Value.x;
                        targetY = coords.Value.y;
                    }
                    else
                    {
                        throw new Exception($"Invalid coordinates value: {step.Value}");
                    }
                }
                else
                {
                    throw new Exception($"{step.Action} step requires either a Selector or coordinates Value.");
                }

                return (targetX.Value, targetY.Value, nodeId);
            }
            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) && retryCount < 2)
            {
                Log($"Resolution failed due to Node ID invalidation: {ex.Message}. Retrying...");
                retryCount++;
                await Task.Delay(100, token);
            }
        }
        throw new Exception($"Failed to resolve coordinates for step.");
    }

    private static (double x, double y)? ParseCoordinates(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var xPart = parts[0].Replace("x:", "").Replace("x=", "").Trim(' ', '"', '\'');
            var yPart = parts[1].Replace("y:", "").Replace("y=", "").Trim(' ', '"', '\'');
            if (double.TryParse(xPart, out double x) && double.TryParse(yPart, out double y))
            {
                return (x, y);
            }
        }
        return null;
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string? value)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(value)) return dict;

        var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOfAny(new[] { ':', '=' });
            if (idx > 0)
            {
                var key = part.Substring(0, idx).Trim();
                var val = part.Substring(idx + 1).Trim();
                dict[key] = CleanValue(val);
            }
        }
        return dict;
    }

    private static string CleanValue(string val)
    {
        if (val == null) return "";
        val = val.Trim();
        if (val.StartsWith("\"") && val.EndsWith("\""))
        {
            val = val.Substring(1, val.Length - 2);
        }
        else if (val.StartsWith("'") && val.EndsWith("'"))
        {
            val = val.Substring(1, val.Length - 2);
        }
        return val;
    }
}
