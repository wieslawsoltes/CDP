using Avalonia;
using Avalonia.Controls;

namespace CdpInspectorApp.Views;

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
