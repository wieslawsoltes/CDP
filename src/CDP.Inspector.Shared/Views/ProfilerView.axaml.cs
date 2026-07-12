using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using CdpInspectorApp.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ProfilerView : UserControl
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

    public Button BtnStartProfiler => btnStartProfiler;
    public Button BtnStopProfiler => btnStopProfiler;
    public Button BtnLoadProfile => btnLoadProfile;
    public DataGrid DgMethodStats => dgMethodStats;

    public ProfilerView()
    {
        InitializeComponent();

        // Initialize view cache
        var pnl1 = this.FindControl<Control>("pnlSessionsList");
        var pnl2 = this.FindControl<Control>("pnlFlameCharts");
        var pnl3 = this.FindControl<Control>("pnlBottomUpCalls");
        var pnl4 = this.FindControl<Control>("pnlMemoryAllocations");
        var pnl5 = this.FindControl<Control>("pnlCallerCallee");
        var hiddenPanel = this.FindControl<Panel>("HiddenPanel");
        if (hiddenPanel != null)
        {
            if (pnl1 != null) { hiddenPanel.Children.Remove(pnl1); _viewsCache["SessionsList"] = pnl1; }
            if (pnl2 != null) { hiddenPanel.Children.Remove(pnl2); _viewsCache["FlameCharts"] = pnl2; }
            if (pnl3 != null) { hiddenPanel.Children.Remove(pnl3); _viewsCache["BottomUpCalls"] = pnl3; }
            if (pnl4 != null) { hiddenPanel.Children.Remove(pnl4); _viewsCache["MemoryAllocations"] = pnl4; }
            if (pnl5 != null) { hiddenPanel.Children.Remove(pnl5); _viewsCache["CallerCallee"] = pnl5; }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel mainVM)
        {
            mainVM.Profiler.SaveFileCallback = async (json) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export CPU Profile",
                        DefaultExtension = "cpuprofile",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("V8 CPU Profile")
                            {
                                Patterns = new[] { "*.cpuprofile" }
                            }
                        }
                    });
                    if (file != null)
                    {
                        using var stream = await file.OpenWriteAsync();
                        using var writer = new StreamWriter(stream);
                        await writer.WriteAsync(json);
                    }
                }
            };

            mainVM.Profiler.OpenFileCallback = async () =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Load CPU Profile",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("V8 CPU Profile")
                            {
                                Patterns = new[] { "*.cpuprofile" }
                            }
                        }
                    });
                    if (files != null && files.Count > 0)
                    {
                        using var stream = await files[0].OpenReadAsync();
                        using var reader = new StreamReader(stream);
                        return await reader.ReadToEndAsync();
                    }
                }
                return null;
            };
        }
    }
}
