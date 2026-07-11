using System;
using System.ComponentModel;
using Avalonia.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class DiffView : UserControl
{
    private MainWindowViewModel? _mainViewModel;
    private DiffViewModel? _diffViewModel;

    public DiffView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_diffViewModel != null)
        {
            _diffViewModel.PropertyChanged -= OnDiffViewModelPropertyChanged;
        }

        _mainViewModel = DataContext as MainWindowViewModel;
        _diffViewModel = _mainViewModel?.Diff;

        if (_diffViewModel != null)
        {
            _diffViewModel.PropertyChanged += OnDiffViewModelPropertyChanged;
            UpdateProportionBar();
        }
    }

    private void OnDiffViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiffViewModel.DiffLines))
        {
            UpdateProportionBar();
        }
    }

    private void UpdateProportionBar()
    {
        if (_diffViewModel == null) return;

        var grid = this.FindControl<Grid>("ProportionBarGrid");
        if (grid == null) return;

        int u = _diffViewModel.LinesUnchangedCount;
        int d = _diffViewModel.LinesDeletedCount;
        int a = _diffViewModel.LinesAddedCount;

        try
        {
            if (u == 0 && d == 0 && a == 0)
            {
                grid.ColumnDefinitions = new ColumnDefinitions("1*,0*,0*");
            }
            else
            {
                grid.ColumnDefinitions = new ColumnDefinitions($"{u}*,{d}*,{a}*");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiffView] Failed to parse column definitions: {ex.Message}");
        }
    }
}
