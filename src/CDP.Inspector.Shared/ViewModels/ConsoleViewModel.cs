using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;
using CDP.Editor.Splits.Models;
using Avalonia.Layout;

namespace CdpInspectorApp.ViewModels;

public class ConsoleViewModel : ViewModelBase, IStateProvider
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ConsoleViewModel>();
    private readonly ICdpService _cdpService;
    private readonly List<LogModel> _allLogs = new();
    private ObservableCollection<LogModel> _logs = new();
    private ObservableCollection<ConsoleItemModel> _consoleHistory = new();
    private string _consoleInputText = "";
    private ObservableCollection<PinnedExpressionViewModel> _pinnedExpressions = new();
    private string _pinnedExpressionInputText = "";
    private readonly DispatcherTimer _watchTimer;
    private bool _isEvaluatingPinned;

    // Filters
    private bool _filterAll = true;
    private bool _filterError;
    private bool _filterWarning;
    private bool _filterInfo;
    private bool _filterVerbose;
    private string _filterQuery = "";

    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;

    public SplitNode? LayoutRoot
    {
        get => _layoutRoot;
        set => RaiseAndSetIfChanged(ref _layoutRoot, value);
    }

    public BoxNode? SelectedPane
    {
        get => _selectedPane;
        set => RaiseAndSetIfChanged(ref _selectedPane, value);
    }

    public void ResetLayout()
    {
        var logs = new BoxNode();
        logs.AddTab("Console Logs", "TerminalIcon", "ConsoleLogs");

        var watch = new BoxNode();
        watch.AddTab("Live Watch", "TimerIcon", "ConsoleWatch");

        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, logs, watch) { SplitterRatio = 0.7 };
        SelectedPane = logs;
    }

    public ObservableCollection<LogModel> Logs => _logs;
    public ObservableCollection<ConsoleItemModel> ConsoleHistory => _consoleHistory;
    public ObservableCollection<PinnedExpressionViewModel> PinnedExpressions => _pinnedExpressions;
    
    public string ConsoleInputText
    {
        get => _consoleInputText;
        set
        {
            if (RaiseAndSetIfChanged(ref _consoleInputText, value))
            {
                ((RelayCommand)EvaluateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string PinnedExpressionInputText
    {
        get => _pinnedExpressionInputText;
        set
        {
            if (RaiseAndSetIfChanged(ref _pinnedExpressionInputText, value))
            {
                ((RelayCommand)AddPinnedExpressionCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand AddPinnedExpressionCommand { get; }
    public ICommand RemovePinnedExpressionCommand { get; }

    public bool FilterAll
    {
        get => _filterAll;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterAll, value))
            {
                if (value)
                {
                    _filterError = false;
                    _filterWarning = false;
                    _filterInfo = false;
                    _filterVerbose = false;
                    OnPropertyChanged(nameof(FilterError));
                    OnPropertyChanged(nameof(FilterWarning));
                    OnPropertyChanged(nameof(FilterInfo));
                    OnPropertyChanged(nameof(FilterVerbose));
                }
                RebuildFilteredLogs();
            }
        }
    }

    public bool FilterError
    {
        get => _filterError;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterError, value))
            {
                if (value) FilterAll = false;
                RebuildFilteredLogs();
            }
        }
    }

    public bool FilterWarning
    {
        get => _filterWarning;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterWarning, value))
            {
                if (value) FilterAll = false;
                RebuildFilteredLogs();
            }
        }
    }

    public bool FilterInfo
    {
        get => _filterInfo;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterInfo, value))
            {
                if (value) FilterAll = false;
                RebuildFilteredLogs();
            }
        }
    }

    public bool FilterVerbose
    {
        get => _filterVerbose;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterVerbose, value))
            {
                if (value) FilterAll = false;
                RebuildFilteredLogs();
            }
        }
    }

    public string FilterQuery
    {
        get => _filterQuery;
        set
        {
            if (RaiseAndSetIfChanged(ref _filterQuery, value))
            {
                RebuildFilteredLogs();
            }
        }
    }

    private readonly Func<TestStudioViewModel?>? _getTestStudioFunc;
    private bool _isUiReplMode;

    public bool IsUiReplMode
    {
        get => _isUiReplMode;
        set
        {
            if (RaiseAndSetIfChanged(ref _isUiReplMode, value))
            {
                OnPropertyChanged(nameof(ConsolePlaceholderText));
            }
        }
    }

    public string ConsolePlaceholderText => IsUiReplMode ? "Enter UI Command (e.g. tap #btnConnect, input #txtUser admin, assert #label)..." : "Enter C# Interactive command (use inspected $0 variable)...";

    public ICommand ClearLogsCommand { get; }
    public ICommand EvaluateCommand { get; }

    public ConsoleViewModel(ICdpService cdpService, Func<TestStudioViewModel?>? getTestStudioFunc = null)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _getTestStudioFunc = getTestStudioFunc;
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        ClearLogsCommand = new RelayCommand(ClearLogs);
        EvaluateCommand = new RelayCommand(async () => await EvaluateAsync(), () => !string.IsNullOrEmpty(ConsoleInputText) && _cdpService.IsConnected);
        AddPinnedExpressionCommand = new RelayCommand(AddPinnedExpression, () => !string.IsNullOrEmpty(PinnedExpressionInputText) && _cdpService.IsConnected);
        RemovePinnedExpressionCommand = new RelayCommand<PinnedExpressionViewModel>(RemovePinnedExpression);

        _watchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _watchTimer.Tick += async (sender, e) => await EvaluatePinnedExpressionsAsync();

        if (_cdpService.IsConnected)
        {
            StartWatchTimer();
        }

        ResetLayout();
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeDomainAsync();
                StartWatchTimer();
            }
            else
            {
                ClearData();
                StopWatchTimer();
            }
            ((RelayCommand)EvaluateCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddPinnedExpressionCommand).RaiseCanExecuteChanged();
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Log.entryAdded" && e.Params != null)
        {
            var entry = e.Params["entry"] as JsonObject;
            if (entry != null)
            {
                string text = entry["text"]?.GetValue<string>() ?? "";
                string level = entry["level"]?.GetValue<string>() ?? "info";
                double timestampMs = entry["timestamp"]?.GetValue<double>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)timestampMs).LocalDateTime;

                Dispatcher.UIThread.Post(() =>
                {
                    var log = new LogModel(timestamp, level, text);
                    _allLogs.Add(log);
                    if (_allLogs.Count > 500) _allLogs.RemoveAt(0);

                    if (MatchesFilter(log))
                    {
                        Logs.Add(log);
                        if (Logs.Count > 100) Logs.RemoveAt(0);
                    }
                });
            }
        }
    }

    private async Task InitializeDomainAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Log.enable");
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ConsoleViewModel", "Error enabling Log domain", ex);
        }
    }

    private void RebuildFilteredLogs()
    {
        if (!_filterAll && !_filterError && !_filterWarning && !_filterInfo && !_filterVerbose)
        {
            _filterAll = true;
            OnPropertyChanged(nameof(FilterAll));
        }

        Dispatcher.UIThread.Post(() =>
        {
            Logs.Clear();
            foreach (var log in _allLogs)
            {
                if (MatchesFilter(log))
                {
                    Logs.Add(log);
                }
            }
        });
    }

    private bool MatchesFilter(LogModel log)
    {
        if (!_filterAll)
        {
            string lvl = log.Level.ToLowerInvariant();
            if (lvl.Contains("err") && !_filterError) return false;
            if (lvl.Contains("warn") && !_filterWarning) return false;
            if ((lvl.Contains("info") || lvl.Contains("log")) && !_filterInfo) return false;
            if (lvl.Contains("verb") && !_filterVerbose) return false;
        }

        if (!string.IsNullOrEmpty(FilterQuery))
        {
            if (!log.Text.Contains(FilterQuery, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _allLogs.Clear();
            Logs.Clear();
            ConsoleHistory.Clear();
            ConsoleInputText = "";
        });
    }

    private void ClearLogs()
    {
        _allLogs.Clear();
        Logs.Clear();
    }

    public async Task EvaluateAsync()
    {
        string expr = ConsoleInputText.Trim();
        if (string.IsNullOrEmpty(expr)) return;

        ConsoleInputText = "";
        ((RelayCommand)EvaluateCommand).RaiseCanExecuteChanged();

        if (IsUiReplMode)
        {
            try
            {
                var parts = expr.Split(' ', 2);
                var action = parts[0].ToLowerInvariant();
                var selectorAndValue = parts.Length > 1 ? parts[1].Trim() : "";

                string selector = "";
                string val = "";

                if (action == "input" || action == "inputtext")
                {
                    var inputParts = selectorAndValue.Split(' ', 2);
                    selector = inputParts[0];
                    val = inputParts.Length > 1 ? inputParts[1] : "";
                }
                else
                {
                    selector = selectorAndValue;
                }

                // Map aliases
                string finalAction = action switch
                {
                    "click" => "tapOn",
                    "tap" => "tapOn",
                    "doubletap" => "doubleTapOn",
                    "longpress" => "longPressOn",
                    "input" => "inputText",
                    "inputtext" => "inputText",
                    "assert" => "assertVisible",
                    "assertvisible" => "assertVisible",
                    "assertnotvisible" => "assertNotVisible",
                    "scroll" => "scrollUntilVisible",
                    "scrolluntilvisible" => "scrollUntilVisible",
                    _ => action
                };

                var step = new TestStudioStepModel { Action = finalAction, Selector = selector, Value = val };

                var testStudio = _getTestStudioFunc?.Invoke();
                if (testStudio != null)
                {
                    await testStudio.AddInteractiveStepAsync(step);
                    ConsoleHistory.Add(new ConsoleItemModel(expr, $"Executed: {finalAction} '{selector}' '{val}'", false));
                }
                else
                {
                    ConsoleHistory.Add(new ConsoleItemModel(expr, "Error: Test Studio ViewModel not initialized", true));
                }
            }
            catch (Exception ex)
            {
                ConsoleHistory.Add(new ConsoleItemModel(expr, $"Error: {ex.Message}", true));
            }

            if (_historyLines.Count == 0 || _historyLines[^1] != expr)
            {
                _historyLines.Add(expr);
            }
            _historyIndex = _historyLines.Count;
            return;
        }

        try
        {
            var res = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = expr,
                ["returnByValue"] = false
            });

            var resultObj = res["result"] as JsonObject;
            string displayResult = "";

            if (resultObj != null)
            {
                if (resultObj.ContainsKey("description"))
                {
                    displayResult = resultObj["description"]?.GetValue<string>() ?? "null";
                }
                else if (resultObj.ContainsKey("value"))
                {
                    displayResult = resultObj["value"]?.ToString() ?? "null";
                }
                else if (resultObj["subtype"]?.GetValue<string>() == "null")
                {
                    displayResult = "null";
                }
                else
                {
                    displayResult = resultObj.ToJsonString();
                }
            }
            else
            {
                displayResult = "Success";
            }

            if (resultObj != null && resultObj.ContainsKey("objectId"))
            {
                var objectId = resultObj["objectId"]?.GetValue<string>();
                var type = resultObj["type"]?.GetValue<string>();
                ConsoleHistory.Add(new ConsoleItemModel(expr, displayResult, false, objectId: objectId, type: type, cdpService: _cdpService));
            }
            else
            {
                ConsoleHistory.Add(new ConsoleItemModel(expr, displayResult, false));
            }
            if (_historyLines.Count == 0 || _historyLines[^1] != expr)
            {
                _historyLines.Add(expr);
            }
            _historyIndex = _historyLines.Count;
        }
        catch (Exception ex)
        {
            ConsoleHistory.Add(new ConsoleItemModel(expr, ex.Message, true));
        }
    }

    private readonly List<string> _historyLines = new();
    private int _historyIndex = -1;

    private ObservableCollection<string> _completions = new();
    private bool _isCompletionActive;
    private int _selectedCompletionIndex = -1;

    public ObservableCollection<string> Completions => _completions;

    public bool IsCompletionActive
    {
        get => _isCompletionActive;
        set => RaiseAndSetIfChanged(ref _isCompletionActive, value);
    }

    public int SelectedCompletionIndex
    {
        get => _selectedCompletionIndex;
        set => RaiseAndSetIfChanged(ref _selectedCompletionIndex, value);
    }

    public string? GetPreviousHistoryLine()
    {
        if (_historyLines.Count == 0) return null;
        if (_historyIndex > 0)
        {
            _historyIndex--;
        }
        return _historyLines[_historyIndex];
    }

    public string? GetNextHistoryLine()
    {
        if (_historyIndex < _historyLines.Count - 1)
        {
            _historyIndex++;
            return _historyLines[_historyIndex];
        }
        _historyIndex = _historyLines.Count;
        return null;
    }

    public async Task QueryCompletionsAsync(string expression, int cursorPosition)
    {
        if (string.IsNullOrEmpty(expression) || !_cdpService.IsConnected)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Completions.Clear();
                IsCompletionActive = false;
            });
            return;
        }

        if (IsUiReplMode)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Completions.Clear();
                int spaceIndex = expression.LastIndexOf(' ', Math.Max(0, cursorPosition - 1));
                if (spaceIndex < 0)
                {
                    var word = expression.Substring(0, cursorPosition).ToLowerInvariant();
                    var commands = new[] { "tap", "click", "inputText", "input", "assertVisible", "assert", "assertNotVisible", "scrollUntilVisible", "scroll", "back" };
                    var matches = commands.Where(c => c.StartsWith(word, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var m in matches)
                    {
                        Completions.Add(m);
                    }
                }
                else
                {
                    var word = expression.Substring(spaceIndex + 1, cursorPosition - (spaceIndex + 1));
                    var selectors = SelectorService.Instance.AvailableSelectors;
                    var matches = selectors.Where(s => s.Contains(word, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var m in matches)
                    {
                        Completions.Add(m);
                    }
                }

                if (Completions.Count > 0)
                {
                    IsCompletionActive = true;
                    SelectedCompletionIndex = 0;
                }
                else
                {
                    IsCompletionActive = false;
                    SelectedCompletionIndex = -1;
                }
            });
            return;
        }

        try
        {
            var res = await _cdpService.SendCommandAsync("Runtime.getCompletions", new JsonObject
            {
                ["expression"] = expression,
                ["cursorPosition"] = cursorPosition
            });

            var completionsArr = res["completions"] as JsonArray;
            Dispatcher.UIThread.Post(() =>
            {
                Completions.Clear();
                if (completionsArr != null && completionsArr.Count > 0)
                {
                    foreach (var itemNode in completionsArr)
                    {
                        var displayText = itemNode?["displayText"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(displayText))
                        {
                            Completions.Add(displayText);
                        }
                    }
                    IsCompletionActive = true;
                    SelectedCompletionIndex = 0;
                }
                else
                {
                    IsCompletionActive = false;
                    SelectedCompletionIndex = -1;
                }
            });
        }
        catch
        {
            Dispatcher.UIThread.Post(() =>
            {
                Completions.Clear();
                IsCompletionActive = false;
                SelectedCompletionIndex = -1;
            });
        }
    }

    private void AddPinnedExpression()
    {
        string expr = PinnedExpressionInputText?.Trim() ?? "";
        if (string.IsNullOrEmpty(expr)) return;

        if (!_pinnedExpressions.Any(x => x.Expression == expr))
        {
            _pinnedExpressions.Add(new PinnedExpressionViewModel
            {
                Expression = expr,
                Result = "Waiting...",
                IsError = false
            });
        }

        PinnedExpressionInputText = "";
        ((RelayCommand)AddPinnedExpressionCommand).RaiseCanExecuteChanged();
        
        _ = EvaluatePinnedExpressionsAsync();
    }

    private void RemovePinnedExpression(PinnedExpressionViewModel item)
    {
        if (item != null)
        {
            _pinnedExpressions.Remove(item);
        }
    }

    private void StartWatchTimer()
    {
        if (!_watchTimer.IsEnabled)
        {
            _watchTimer.Start();
        }
    }

    private void StopWatchTimer()
    {
        if (_watchTimer.IsEnabled)
        {
            _watchTimer.Stop();
        }
    }

    private async Task EvaluatePinnedExpressionsAsync()
    {
        if (_isEvaluatingPinned || !_cdpService.IsConnected || _pinnedExpressions.Count == 0) return;
        _isEvaluatingPinned = true;
        try
        {
            var list = _pinnedExpressions.ToList();
            foreach (var item in list)
            {
                try
                {
                    var res = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
                    {
                        ["expression"] = item.Expression,
                        ["returnByValue"] = false
                    });
                    
                    var resultObj = res["result"] as JsonObject;
                    string displayResult = "";
                    bool isError = false;

                    if (resultObj != null)
                    {
                        if (resultObj.ContainsKey("description"))
                        {
                            displayResult = resultObj["description"]?.GetValue<string>() ?? "null";
                        }
                        else if (resultObj.ContainsKey("value"))
                        {
                            displayResult = resultObj["value"]?.ToString() ?? "null";
                        }
                        else if (resultObj["subtype"]?.GetValue<string>() == "null")
                        {
                            displayResult = "null";
                        }
                        else
                        {
                            displayResult = resultObj.ToJsonString();
                        }
                    }
                    else
                    {
                        displayResult = "Success";
                    }
                    
                    item.Result = displayResult;
                    item.IsError = isError;
                }
                catch (Exception ex)
                {
                    item.Result = ex.Message;
                    item.IsError = true;
                }
            }
        }
        finally
        {
            _isEvaluatingPinned = false;
        }
    }

    #region IStateProvider Implementation

    public string StateKey => "console";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["filterAll"] = FilterAll;
        root["filterError"] = FilterError;
        root["filterWarning"] = FilterWarning;
        root["filterInfo"] = FilterInfo;
        root["filterVerbose"] = FilterVerbose;
        root["filterQuery"] = FilterQuery;
        root["consoleInputText"] = ConsoleInputText;

        var pinnedArray = new JsonArray();
        foreach (var pinned in PinnedExpressions)
        {
            if (!string.IsNullOrEmpty(pinned.Expression))
            {
                pinnedArray.Add(pinned.Expression);
            }
        }
        root["pinnedExpressions"] = pinnedArray;

        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("filterAll", out var allNode) && allNode != null)
        {
            FilterAll = (bool?)allNode ?? true;
        }
        if (json.TryGetPropertyValue("filterError", out var errorNode) && errorNode != null)
        {
            FilterError = (bool?)errorNode ?? false;
        }
        if (json.TryGetPropertyValue("filterWarning", out var warnNode) && warnNode != null)
        {
            FilterWarning = (bool?)warnNode ?? false;
        }
        if (json.TryGetPropertyValue("filterInfo", out var infoNode) && infoNode != null)
        {
            FilterInfo = (bool?)infoNode ?? false;
        }
        if (json.TryGetPropertyValue("filterVerbose", out var verboseNode) && verboseNode != null)
        {
            FilterVerbose = (bool?)verboseNode ?? false;
        }
        if (json.TryGetPropertyValue("filterQuery", out var queryNode) && queryNode != null)
        {
            FilterQuery = (string?)queryNode ?? "";
        }
        if (json.TryGetPropertyValue("consoleInputText", out var inputNode) && inputNode != null)
        {
            ConsoleInputText = (string?)inputNode ?? "";
        }

        if (json.TryGetPropertyValue("pinnedExpressions", out var pinnedNode) && pinnedNode is JsonArray pinnedArray)
        {
            PinnedExpressions.Clear();
            foreach (var item in pinnedArray)
            {
                var expr = (string?)item;
                if (!string.IsNullOrEmpty(expr))
                {
                    PinnedExpressions.Add(new PinnedExpressionViewModel { Expression = expr });
                }
            }
        }
    }

    #endregion
}
