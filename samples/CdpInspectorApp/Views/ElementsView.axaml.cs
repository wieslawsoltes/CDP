using Avalonia.Controls;

namespace CdpInspectorApp.Views;

public partial class ElementsView : UserControl
{
    public TextBox TxtSearch => txtSearch;
    public Button BtnSearch => btnSearch;
    public TreeView TreeDom => treeDom;
    public TextBlock TxtSelectedNodeId => txtSelectedNodeId;
    public Button BtnFocus => btnFocus;
    public CheckBox ChkHighlight => chkHighlight;
    public Button BtnDeleteControl => btnDeleteControl;

    public ListBox ListCssProperties => listCssProperties;
    public TextBox TxtStyleText => txtStyleText;
    public Button BtnApplyStyleText => btnApplyStyleText;

    public ListBox ListComputedStyles => listComputedStyles;

    public ListBox ListAttributes => listAttributes;
    public TextBox TxtAttrName => txtAttrName;
    public TextBox TxtAttrValue => txtAttrValue;
    public Button BtnApplyAttr => btnApplyAttr;
    public Button BtnDeleteAttr => btnDeleteAttr;

    public ListBox ListProperties => listProperties;
    public TextBlock LblSelectedProperty => lblSelectedProperty;
    public TextBox TxtPropertyValue => txtPropertyValue;
    public Button BtnApplyProperty => btnApplyProperty;

    public ListBox ListEventListeners => listEventListeners;

    public ElementsView()
    {
        InitializeComponent();
    }
}
