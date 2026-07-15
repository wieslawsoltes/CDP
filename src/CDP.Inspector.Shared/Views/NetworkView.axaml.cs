using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class NetworkView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

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

    public Button BtnClearNetwork => btnNetworkClear;
    public TextBox TxtNetworkFilter => txtNetworkFilter;
    public DataGrid LstNetworkRequests => lstNetworkRequests;
    public TextBlock LblNetUrl => lblNetUrl;
    public TextBox TxtNetReqHeaders => txtNetReqHeaders;
    public TextBox TxtNetResHeaders => txtNetResHeaders;
    public TextBox TxtNetBody => txtNetBody;

    public NetworkView()
    {
        InitializeComponent();

        // Initialize view cache
        var pnl1 = this.FindControl<Control>("lstNetworkRequests");
        var pnl2 = this.FindControl<Control>("pnlRequestDetails");
        var pnl3 = this.FindControl<Control>("pnlMockingRules");
        var hiddenPanel = this.FindControl<Panel>("HiddenPanel");
        if (hiddenPanel != null)
        {
            if (pnl1 != null) { hiddenPanel.Children.Remove(pnl1); _viewsCache["RequestsList"] = pnl1; }
            if (pnl2 != null) { hiddenPanel.Children.Remove(pnl2); _viewsCache["RequestDetails"] = pnl2; }
            if (pnl3 != null) { hiddenPanel.Children.Remove(pnl3); _viewsCache["MockingRules"] = pnl3; }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
    }
}
