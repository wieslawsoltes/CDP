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

public class AuditsViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private readonly Action<int> _selectNodeInDomTree;

    private int _accessibilityScore = 100;
    private int _bestPracticesScore = 100;
    private int _layoutScore = 100;
    private bool _isAuditing;
    private ObservableCollection<AuditIssueModel> _issues = new();
    private AuditIssueModel? _selectedIssue;

    public int AccessibilityScore
    {
        get => _accessibilityScore;
        private set => RaiseAndSetIfChanged(ref _accessibilityScore, value);
    }

    public int BestPracticesScore
    {
        get => _bestPracticesScore;
        private set => RaiseAndSetIfChanged(ref _bestPracticesScore, value);
    }

    public int LayoutScore
    {
        get => _layoutScore;
        private set => RaiseAndSetIfChanged(ref _layoutScore, value);
    }

    public bool IsAuditing
    {
        get => _isAuditing;
        private set => RaiseAndSetIfChanged(ref _isAuditing, value);
    }

    public ObservableCollection<AuditIssueModel> Issues => _issues;

    public AuditIssueModel? SelectedIssue
    {
        get => _selectedIssue;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedIssue, value))
            {
                ((RelayCommand)InspectIssueCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Score colors matching Lighthouse gauge color ranges (0-49 red, 50-89 orange, 90-100 green)
    public string A11yBrush => GetScoreColor(AccessibilityScore);
    public string BestPracticesBrush => GetScoreColor(BestPracticesScore);
    public string LayoutBrush => GetScoreColor(LayoutScore);

    public ICommand RunAuditsCommand { get; }
    public ICommand InspectIssueCommand { get; }

    public AuditsViewModel(ICdpService cdpService, Action<int> selectNodeInDomTree)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _selectNodeInDomTree = selectNodeInDomTree ?? throw new ArgumentNullException(nameof(selectNodeInDomTree));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        RunAuditsCommand = new RelayCommand(async () => await RunAuditsAsync(), () => _cdpService.IsConnected && !IsAuditing);
        InspectIssueCommand = new RelayCommand(InspectIssue, () => SelectedIssue != null);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected)
            {
                ClearData();
            }
            ((RelayCommand)RunAuditsCommand).RaiseCanExecuteChanged();
        }
    }

    public async Task RunAuditsAsync()
    {
        if (!_cdpService.IsConnected || IsAuditing) return;

        IsAuditing = true;
        ((RelayCommand)RunAuditsCommand).RaiseCanExecuteChanged();

        try
        {
            // Call our custom runDiagnostics endpoint on target's Audits domain
            var response = await _cdpService.SendCommandAsync("Audits.runDiagnostics");
            if (response != null)
            {
                int a11y = response["accessibilityScore"]?.GetValue<int>() ?? 100;
                int best = response["bestPracticesScore"]?.GetValue<int>() ?? 100;
                int layout = response["layoutScore"]?.GetValue<int>() ?? 100;
                var issueList = response["issues"] as JsonArray;

                Dispatcher.UIThread.Post(() =>
                {
                    AccessibilityScore = a11y;
                    BestPracticesScore = best;
                    LayoutScore = layout;
                    
                    OnPropertyChanged(nameof(A11yBrush));
                    OnPropertyChanged(nameof(BestPracticesBrush));
                    OnPropertyChanged(nameof(LayoutBrush));

                    Issues.Clear();
                    if (issueList != null)
                    {
                        foreach (var node in issueList)
                        {
                            if (node is JsonObject obj)
                            {
                                Issues.Add(new AuditIssueModel
                                {
                                    Category = obj["category"]?.GetValue<string>() ?? "",
                                    Severity = obj["severity"]?.GetValue<string>() ?? "info",
                                    NodeId = obj["nodeId"]?.GetValue<int>() ?? 0,
                                    ControlType = obj["controlType"]?.GetValue<string>() ?? "",
                                    Message = obj["message"]?.GetValue<string>() ?? ""
                                });
                            }
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running visual audits: {ex.Message}");
        }
        finally
        {
            IsAuditing = false;
            Dispatcher.UIThread.Post(() =>
            {
                ((RelayCommand)RunAuditsCommand).RaiseCanExecuteChanged();
            });
        }
    }

    private void InspectIssue()
    {
        if (SelectedIssue != null)
        {
            _selectNodeInDomTree(SelectedIssue.NodeId);
        }
    }

    private string GetScoreColor(int score)
    {
        if (score >= 90) return "#0ccc5a"; // green
        if (score >= 50) return "#ff9800"; // orange
        return "#ff3333";                  // red
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AccessibilityScore = 100;
            BestPracticesScore = 100;
            LayoutScore = 100;
            Issues.Clear();
            SelectedIssue = null;
        });
    }
}
