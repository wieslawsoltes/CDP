using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class ConsoleViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private readonly List<LogModel> _allLogs = new();
    private ObservableCollection<LogModel> _logs = new();
    private ObservableCollection<ConsoleItemModel> _consoleHistory = new();
    private string _consoleInputText = "";

    // Filters
    private bool _filterAll = true;
    private bool _filterError;
    private bool _filterWarning;
    private bool _filterInfo;
    private bool _filterVerbose;
    private string _filterQuery = "";

    public ObservableCollection<LogModel> Logs => _logs;
    public ObservableCollection<ConsoleItemModel> ConsoleHistory => _consoleHistory;

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

    public ICommand ClearLogsCommand { get; }
    public ICommand EvaluateCommand { get; }

    public ConsoleViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        ClearLogsCommand = new RelayCommand(ClearLogs);
        EvaluateCommand = new RelayCommand(async () => await EvaluateAsync(), () => !string.IsNullOrEmpty(ConsoleInputText) && _cdpService.IsConnected);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeDomainAsync();
            }
            else
            {
                ClearData();
            }
            ((RelayCommand)EvaluateCommand).RaiseCanExecuteChanged();
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
            Console.WriteLine($"Error enabling Log domain: {ex.Message}");
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

            ConsoleHistory.Add(new ConsoleItemModel(expr, displayResult, false));
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
}
