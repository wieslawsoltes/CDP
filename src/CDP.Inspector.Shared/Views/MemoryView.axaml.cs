using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class MemoryView : UserControl
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

    public MemoryView()
    {
        InitializeComponent();

        // Initialize view cache
        var pnl1 = this.FindControl<Control>("pnlSnapshotsList");
        var pnl2 = this.FindControl<Control>("pnlSnapshotOverview");
        var pnl3 = this.FindControl<Control>("pnlDetachedControls");
        var hiddenPanel = this.FindControl<Panel>("HiddenPanel");
        if (hiddenPanel != null)
        {
            if (pnl1 != null) { hiddenPanel.Children.Remove(pnl1); _viewsCache["SnapshotsList"] = pnl1; }
            if (pnl2 != null) { hiddenPanel.Children.Remove(pnl2); _viewsCache["SnapshotOverview"] = pnl2; }
            if (pnl3 != null) { hiddenPanel.Children.Remove(pnl3); _viewsCache["DetachedControls"] = pnl3; }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel mainVM)
        {
            mainVM.Memory.SaveFileCallback = async (json) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export Heap Snapshot",
                        DefaultExtension = "heapsnapshot",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("V8 Heap Snapshot")
                            {
                                Patterns = new[] { "*.heapsnapshot" }
                            }
                        }
                    });
                    if (file != null)
                    {
                        using var stream = await file.OpenWriteAsync();
                        using var writer = new System.IO.StreamWriter(stream);
                        await writer.WriteAsync(json);
                    }
                }
            };
        }
    }
}
