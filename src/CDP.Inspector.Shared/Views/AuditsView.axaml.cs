using Avalonia.Controls;
using CDP.Editor.Splits.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class AuditsView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    private void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }
        else if (control.Parent is SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
            splitBox.UpdateLayout();
        }
    }

    public AuditsView()
    {
        InitializeComponent();

        var issuesPanel = AuditIssuesPanel;
        var detailsPanel = AuditDetailsPanel;

        HiddenPanel.Children.Clear();

        _viewsCache["AuditIssues"] = issuesPanel;
        _viewsCache["AuditDetails"] = detailsPanel;

        SplitControl.ViewResolver = (viewName, targetBox) =>
        {
            if (_viewsCache.TryGetValue(viewName, out var cached))
            {
                if (targetBox == null || cached.Parent != targetBox)
                {
                    DetachControl(cached);
                }
                return cached;
            }
            return new Control();
        };
    }
}
