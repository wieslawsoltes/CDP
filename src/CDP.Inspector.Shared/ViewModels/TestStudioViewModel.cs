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

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StepOverCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddTapCommand { get; }
    public ICommand AddInputCommand { get; }
    public ICommand AddAssertVisibleCommand { get; }
    public ICommand AddAssertNotVisibleCommand { get; }
    public ICommand AddClearTextCommand { get; }
    public ICommand AddDelayCommand { get; }
    public ICommand AddLaunchAppCommand { get; }
    public ICommand AddBackCommand { get; }
    public ICommand AddScrollCommand { get; }
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
        AddInputCommand = new RelayCommand(async () => await AddInputAsync());
        AddAssertVisibleCommand = new RelayCommand(async () => await AddAssertVisibleAsync());
        AddAssertNotVisibleCommand = new RelayCommand(async () => await AddAssertNotVisibleAsync());
        AddClearTextCommand = new RelayCommand(async () => await AddClearTextAsync());
        AddDelayCommand = new RelayCommand(AddDelay);
        AddLaunchAppCommand = new RelayCommand(AddLaunchApp);
        AddBackCommand = new RelayCommand(async () => await AddBackAsync());
        AddScrollCommand = new RelayCommand(async () => await AddScrollAsync());

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
                        throw new Exception("tapOn step requires either a Selector or coordinates Value.");
                    }

                    if (targetX.HasValue && targetY.HasValue)
                    {
                        if (nodeId > 0)
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }

                        Log($"Tapping at coordinate ({targetX.Value:F1}, {targetY.Value:F1})");

                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                        {
                            ["type"] = "mousePressed",
                            ["x"] = targetX.Value,
                            ["y"] = targetY.Value,
                            ["button"] = "left",
                            ["clickCount"] = 1,
                            ["modifiers"] = 0
                        });
                        await Task.Delay(50, token);

                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                        {
                            ["type"] = "mouseReleased",
                            ["x"] = targetX.Value,
                            ["y"] = targetY.Value,
                            ["button"] = "left",
                            ["clickCount"] = 1,
                            ["modifiers"] = 0
                        });
                        await Task.Delay(200, token);
                    }
                    break;
                }
            case "inputText":
                {
                    if (string.IsNullOrEmpty(step.Selector))
                    {
                        throw new Exception("inputText step requires a Selector.");
                    }
                    Log($"Waiting for element '{step.Selector}' to be visible...");
                    int nodeId = await WaitForElementVisibleAsync(step.Selector, token);

                    Log($"Focusing element and typing '{step.Value}'");
                    await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                    await Task.Delay(100, token);
                    await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = step.Value ?? "" });
                    await Task.Delay(200, token);
                    break;
                }
            case "clearText":
                {
                    if (string.IsNullOrEmpty(step.Selector))
                    {
                        throw new Exception("clearText step requires a Selector.");
                    }
                    Log($"Waiting for element '{step.Selector}' to be visible...");
                    int nodeId = await WaitForElementVisibleAsync(step.Selector, token);

                    Log("Focusing element and clearing text...");
                    await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                    await Task.Delay(100, token);

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
                        ["modifiers"] = 8
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "a",
                        ["modifiers"] = 8
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
        var step = new TestStudioStepModel
        {
            Action = "tapOn",
            Selector = SelectedElementSelector,
            Value = ""
        };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddInputAsync()
    {
        var step = new TestStudioStepModel
        {
            Action = "inputText",
            Selector = SelectedElementSelector,
            Value = InputSimText
        };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertVisibleAsync()
    {
        var step = new TestStudioStepModel
        {
            Action = "assertVisible",
            Selector = SelectedElementSelector,
            Value = ""
        };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertNotVisibleAsync()
    {
        var step = new TestStudioStepModel
        {
            Action = "assertNotVisible",
            Selector = SelectedElementSelector,
            Value = ""
        };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddClearTextAsync()
    {
        var step = new TestStudioStepModel
        {
            Action = "clearText",
            Selector = SelectedElementSelector,
            Value = ""
        };
        await AddInteractiveStepAsync(step);
    }

    public void AddDelay()
    {
        Steps.Add(new TestStudioStepModel
        {
            Action = "delay",
            Selector = "",
            Value = DelayMs.ToString()
        });
    }

    public void AddLaunchApp()
    {
        Steps.Add(new TestStudioStepModel
        {
            Action = "launchApp",
            Selector = "",
            Value = ""
        });
    }

    public async Task AddBackAsync()
    {
        var step = new TestStudioStepModel
        {
            Action = "back",
            Selector = "",
            Value = ""
        };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddScrollAsync()
    {
        var step = new TestStudioStepModel
        {
            Action = "scroll",
            Selector = SelectedElementSelector,
            Value = "direction: down, amount: 100"
        };
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

        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
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
