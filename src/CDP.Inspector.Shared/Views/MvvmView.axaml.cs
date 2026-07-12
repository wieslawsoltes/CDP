using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using System.Collections.Generic;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TreeView and DataGrid are not trim-safe")]
public partial class MvvmView : UserControl
{
    private readonly Dictionary<string, Control> _viewsCache = new();

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
            splitBox.UpdateLayout();
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

    public MvvmView()
    {
        InitializeComponent();

        // Initialize view cache
        var pnl1 = this.FindControl<Control>("pnlMvvmTree");
        var pnl2 = this.FindControl<Control>("pnlMvvmProperties");
        var pnl3 = this.FindControl<Control>("pnlCommandHistory");

        var hiddenPanel = this.FindControl<Panel>("HiddenPanel");
        if (hiddenPanel != null)
        {
            if (pnl1 != null) { hiddenPanel.Children.Remove(pnl1); _viewsCache["MvvmTree"] = pnl1; }
            if (pnl2 != null) { hiddenPanel.Children.Remove(pnl2); _viewsCache["MvvmProperties"] = pnl2; }
            if (pnl3 != null) { hiddenPanel.Children.Remove(pnl3); _viewsCache["CommandHistory"] = pnl3; }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
    }
}
