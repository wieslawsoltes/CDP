using Avalonia;
using Avalonia.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ElementsView : UserControl
{
    public TextBox TxtSearch => txtSearch;
    public Button BtnSearch => btnSearch;
    public DataGrid TreeDom => treeDom;
    public TextBlock TxtSelectedNodeId => txtSelectedNodeId;
    public Button BtnFocus => btnFocus;
    public CheckBox ChkHighlight => chkHighlight;
    public Button BtnDeleteControl => btnDeleteControl;

    public DataGrid ListCssProperties => listCssProperties;
    public TextBox TxtStyleText => txtStyleText;
    public Button BtnApplyStyleText => btnApplyStyleText;

    public DataGrid ListComputedStyles => listComputedStyles;

    public DataGrid ListAttributes => listAttributes;
    public TextBox TxtAttrName => txtAttrName;
    public TextBox TxtAttrValue => txtAttrValue;
    public Button BtnApplyAttr => btnApplyAttr;
    public Button BtnDeleteAttr => btnDeleteAttr;

    public DataGrid ListProperties => listProperties;
    public TextBlock LblSelectedProperty => lblSelectedProperty;
    public TextBox TxtPropertyValue => txtPropertyValue;
    public Button BtnApplyProperty => btnApplyProperty;

    public DataGrid ListEventListeners => listEventListeners;

    public ElementsView()
    {
        InitializeComponent();
        treeDom.SelectionChanged += (s, e) => ScrollSelectedIntoView();

        listCssProperties.CellEditEnded += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is CdpInspectorApp.Models.CssPropertyModel cssProp)
            {
                if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
                {
                    _ = mvm.Elements.UpdateCssPropertyAsync(cssProp);
                }
                else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel evm)
                {
                    _ = evm.UpdateCssPropertyAsync(cssProp);
                }
            }
        };

        listAttributes.CellEditEnded += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is CdpInspectorApp.Models.AttributeModel attr)
            {
                if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
                {
                    _ = mvm.Elements.UpdateAttributeAsync(attr);
                }
                else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel evm)
                {
                    _ = evm.UpdateAttributeAsync(attr);
                }
            }
        };

        listProperties.CellEditEnded += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is CdpInspectorApp.Models.PropertyModel prop)
            {
                if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
                {
                    _ = mvm.Elements.UpdatePropertyAsync(prop);
                }
                else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel evm)
                {
                    _ = evm.UpdatePropertyAsync(prop);
                }
            }
        };
    }

    private void OnBoxValueDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control control && control.Tag is string field)
        {
            CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
            if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
            {
                evm = mvm.Elements;
            }
            else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
            {
                evm = directEvm;
            }

            if (evm != null)
            {
                evm.StartEdit(field);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var textBox = this.Find<TextBox>("txt" + field);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }, Avalonia.Threading.DispatcherPriority.Input);
            }
        }
    }

    private void OnBoxTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
        if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
        {
            evm = mvm.Elements;
        }
        else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
        {
            evm = directEvm;
        }

        if (evm != null)
        {
            if (e.Key == Avalonia.Input.Key.Enter && sender is TextBox textBox && textBox.Tag is string field)
            {
                _ = evm.CommitEditAsync(field);
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                evm.CancelAllEdits();
            }
        }
    }

    private void OnBoxTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CdpInspectorApp.ViewModels.ElementsViewModel? evm = null;
        if (DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel mvm)
        {
            evm = mvm.Elements;
        }
        else if (DataContext is CdpInspectorApp.ViewModels.ElementsViewModel directEvm)
        {
            evm = directEvm;
        }

        if (evm != null && sender is TextBox textBox && textBox.Tag is string field)
        {
            _ = evm.CommitEditAsync(field);
        }
    }

    private void ScrollSelectedIntoView()
    {
        var selected = treeDom.SelectedItem;
        if (selected == null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            treeDom.ScrollIntoView(selected, null);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
