using System;
using Avalonia.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ApplicationView : UserControl
{
    private MainWindowViewModel? _viewModel;

    public ApplicationView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.Application.TableColumns.CollectionChanged -= TableColumns_CollectionChanged;
            _viewModel.Application.ConsoleColumns.CollectionChanged -= ConsoleColumns_CollectionChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel != null)
        {
            _viewModel.Application.TableColumns.CollectionChanged += TableColumns_CollectionChanged;
            _viewModel.Application.ConsoleColumns.CollectionChanged += ConsoleColumns_CollectionChanged;

            // Recreate initially
            RecreateColumns(this.FindControl<DataGrid>("dgActiveTable"), _viewModel.Application.TableColumns);
            RecreateColumns(this.FindControl<DataGrid>("dgConsoleResult"), _viewModel.Application.ConsoleColumns);
        }
    }

    private void TableColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            RecreateColumns(this.FindControl<DataGrid>("dgActiveTable"), _viewModel.Application.TableColumns);
        }
    }

    private void ConsoleColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            RecreateColumns(this.FindControl<DataGrid>("dgConsoleResult"), _viewModel.Application.ConsoleColumns);
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
