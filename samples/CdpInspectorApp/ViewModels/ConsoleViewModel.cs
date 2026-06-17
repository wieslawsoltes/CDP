using System;
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
    private ObservableCollection<LogModel> _logs = new();
    private ObservableCollection<ConsoleItemModel> _consoleHistory = new();
    private string _consoleInputText = "";

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
                    Logs.Add(new LogModel(timestamp, level, text));
                    if (Logs.Count > 100) Logs.RemoveAt(0);
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

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Logs.Clear();
            ConsoleHistory.Clear();
            ConsoleInputText = "";
        });
    }

    private void ClearLogs()
    {
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
        }
        catch (Exception ex)
        {
            ConsoleHistory.Add(new ConsoleItemModel(expr, ex.Message, true));
        }
    }
}
