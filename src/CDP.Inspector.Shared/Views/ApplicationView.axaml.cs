using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ApplicationView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private readonly Dictionary<string, Control> _viewsCache = new();
    private DataGrid? _dgActiveTable;
    private DataGrid? _dgConsoleResult;
    private DataGrid? _dgIndexedDBRecords;

    private Control GetOrCreateViewInstance(string viewName, CDP.Editor.Splits.Controls.SuperSplitBox? targetBox = null)
    {
        if (_viewsCache.TryGetValue(viewName, out var cached))
        {
            if (targetBox == null || cached.Parent != targetBox)
            {
                DetachControl(cached);
            }
            return cached;
        }
        return new TextBlock { Text = $"View {viewName} not found", Margin = new Thickness(10) };
    }

    private void DetachControl(Control control)
    {
        if (control.Parent is CDP.Editor.Splits.Controls.SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
        }
        else if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }

        var visualParent = control.GetVisualParent();
        if (visualParent is ContentPresenter presenter)
        {
            presenter.Content = null;
        }
        else if (visualParent is Panel visualPanel)
        {
            visualPanel.Children.Remove(control);
        }
    }

    public ApplicationView()
    {
        InitializeComponent();

        // Cache control references before detaching
        _dgActiveTable = this.FindControl<DataGrid>("dgActiveTable");
        _dgConsoleResult = this.FindControl<DataGrid>("dgConsoleResult");
        _dgIndexedDBRecords = this.FindControl<DataGrid>("dgIndexedDBRecords");

        // Initialize view cache
        var pnl1 = this.FindControl<Control>("pnlNavigation");
        var pnl2 = this.FindControl<Control>("gridResourceEditor");
        var pnl3 = this.FindControl<Control>("gridStorageEditor");
        var pnl4 = this.FindControl<Control>("gridCookieEditor");
        var pnl5 = this.FindControl<Control>("gridDatabaseViewer");
        var pnl6 = this.FindControl<Control>("gridBackgroundServices");
        var pnl7 = this.FindControl<Control>("gridIndexedDBExplorer");
        var pnl8 = this.FindControl<Control>("pnlSimulator");

        var hiddenPanel = this.FindControl<Panel>("HiddenPanel");
        if (hiddenPanel != null)
        {
            if (pnl1 != null) { hiddenPanel.Children.Remove(pnl1); _viewsCache["Navigation"] = pnl1; }
            if (pnl2 != null) { hiddenPanel.Children.Remove(pnl2); _viewsCache["ResourceEditor"] = pnl2; }
            if (pnl3 != null) { hiddenPanel.Children.Remove(pnl3); _viewsCache["StorageEditor"] = pnl3; }
            if (pnl4 != null) { hiddenPanel.Children.Remove(pnl4); _viewsCache["CookieEditor"] = pnl4; }
            if (pnl5 != null) { hiddenPanel.Children.Remove(pnl5); _viewsCache["DatabaseViewer"] = pnl5; }
            if (pnl6 != null) { hiddenPanel.Children.Remove(pnl6); _viewsCache["BackgroundServices"] = pnl6; }
            if (pnl7 != null) { hiddenPanel.Children.Remove(pnl7); _viewsCache["IndexedDBExplorer"] = pnl7; }
            if (pnl8 != null) { hiddenPanel.Children.Remove(pnl8); _viewsCache["Simulator"] = pnl8; }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.Application.TableColumns.CollectionChanged -= TableColumns_CollectionChanged;
            _viewModel.Application.ConsoleColumns.CollectionChanged -= ConsoleColumns_CollectionChanged;
            _viewModel.Application.IndexedDBColumns.CollectionChanged -= IndexedDBColumns_CollectionChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel != null)
        {
            _viewModel.Application.TableColumns.CollectionChanged += TableColumns_CollectionChanged;
            _viewModel.Application.ConsoleColumns.CollectionChanged += ConsoleColumns_CollectionChanged;
            _viewModel.Application.IndexedDBColumns.CollectionChanged += IndexedDBColumns_CollectionChanged;

            // Recreate initially
            RecreateColumns(_dgActiveTable, _viewModel.Application.TableColumns);
            RecreateColumns(_dgConsoleResult, _viewModel.Application.ConsoleColumns);
            RecreateColumns(_dgIndexedDBRecords, _viewModel.Application.IndexedDBColumns);
        }
    }

    private void TableColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            RecreateColumns(_dgActiveTable, _viewModel.Application.TableColumns);
        }
    }

    private void ConsoleColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            RecreateColumns(_dgConsoleResult, _viewModel.Application.ConsoleColumns);
        }
    }

    private void IndexedDBColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            RecreateColumns(_dgIndexedDBRecords, _viewModel.Application.IndexedDBColumns);
        }
    }

    private void RecreateColumns(DataGrid? dataGrid, System.Collections.Generic.IList<string> columns)
    {
        if (dataGrid == null) return;
        dataGrid.Columns.Clear();
        for (int i = 0; i < columns.Count; i++)
        {
            var columnName = columns[i];
            var column = new DataGridTextColumn
            {
                Header = columnName,
                Binding = new Avalonia.Data.Binding($"[{i}]")
            };
            dataGrid.Columns.Add(column);
        }
    }
}
