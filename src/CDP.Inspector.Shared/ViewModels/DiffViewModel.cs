using System;
using System.Collections.Generic;
using System.Windows.Input;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class DiffViewModel : ViewModelBase
{
    private string _leftTitle = "Original / Request";
    private string _rightTitle = "Modified / Response";
    private string _leftText = "";
    private string _rightText = "";
    private bool _isInlineMode = false;
    private List<DiffLine> _diffLines = new();

    private int _linesAddedCount;
    private int _linesDeletedCount;
    private int _linesUnchangedCount;
    private double _percentAdded;
    private double _percentDeleted;

    public string LeftTitle
    {
        get => _leftTitle;
        set => RaiseAndSetIfChanged(ref _leftTitle, value);
    }

    public string RightTitle
    {
        get => _rightTitle;
        set => RaiseAndSetIfChanged(ref _rightTitle, value);
    }

    public string LeftText
    {
        get => _leftText;
        set
        {
            if (RaiseAndSetIfChanged(ref _leftText, value))
            {
                UpdateDiff();
            }
        }
    }

    public string RightText
    {
        get => _rightText;
        set
        {
            if (RaiseAndSetIfChanged(ref _rightText, value))
            {
                UpdateDiff();
            }
        }
    }

    public bool IsInlineMode
    {
        get => _isInlineMode;
        set => RaiseAndSetIfChanged(ref _isInlineMode, value);
    }

    public List<DiffLine> DiffLines
    {
        get => _diffLines;
        private set => RaiseAndSetIfChanged(ref _diffLines, value);
    }

    public int LinesAddedCount
    {
        get => _linesAddedCount;
        private set => RaiseAndSetIfChanged(ref _linesAddedCount, value);
    }

    public int LinesDeletedCount
    {
        get => _linesDeletedCount;
        private set => RaiseAndSetIfChanged(ref _linesDeletedCount, value);
    }

    public int LinesUnchangedCount
    {
        get => _linesUnchangedCount;
        private set => RaiseAndSetIfChanged(ref _linesUnchangedCount, value);
    }

    public double PercentAdded
    {
        get => _percentAdded;
        private set => RaiseAndSetIfChanged(ref _percentAdded, value);
    }

    public double PercentDeleted
    {
        get => _percentDeleted;
        private set => RaiseAndSetIfChanged(ref _percentDeleted, value);
    }

    private string _proportionColumnDefinitions = "1*,0*,0*";
    public string ProportionColumnDefinitions
    {
        get => _proportionColumnDefinitions;
        private set => RaiseAndSetIfChanged(ref _proportionColumnDefinitions, value);
    }

    public ICommand ToggleDiffModeCommand { get; }
    public ICommand ClearDiffCommand { get; }

    public DiffViewModel()
    {
        ToggleDiffModeCommand = new RelayCommand(() => IsInlineMode = !IsInlineMode);
        ClearDiffCommand = new RelayCommand(() =>
        {
            LeftText = "";
            RightText = "";
        });
    }

    public void SetCompareTexts(string leftTitle, string leftContent, string rightTitle, string rightContent)
    {
        _leftTitle = leftTitle;
        _rightTitle = rightTitle;
        _leftText = leftContent;
        _rightText = rightContent;

        OnPropertyChanged(nameof(LeftTitle));
        OnPropertyChanged(nameof(RightTitle));
        OnPropertyChanged(nameof(LeftText));
        OnPropertyChanged(nameof(RightText));

        UpdateDiff();
    }

    private void UpdateDiff()
    {
        var lines = DiffEngine.ComputeDiff(LeftText, RightText);
        DiffLines = lines;

        int added = 0;
        int deleted = 0;
        int unchanged = 0;

        foreach (var line in lines)
        {
            if (line.Type == DiffType.Added) added++;
            else if (line.Type == DiffType.Deleted) deleted++;
            else unchanged++;
        }

        LinesAddedCount = added;
        LinesDeletedCount = deleted;
        LinesUnchangedCount = unchanged;

        double total = added + deleted + unchanged;
        if (total > 0)
        {
            PercentAdded = (added / total) * 100.0;
            PercentDeleted = (deleted / total) * 100.0;
        }
        else
        {
            PercentAdded = 0;
            PercentDeleted = 0;
        }

        int uVal = unchanged > 0 ? unchanged : 0;
        int dVal = deleted > 0 ? deleted : 0;
        int aVal = added > 0 ? added : 0;
        if (uVal == 0 && dVal == 0 && aVal == 0)
        {
            ProportionColumnDefinitions = "1*,0*,0*";
        }
        else
        {
            ProportionColumnDefinitions = $"{uVal}*,{dVal}*,{aVal}*";
        }
    }
}
