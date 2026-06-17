using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CdpInspectorApp.Views;

public partial class ApplicationView : UserControl
{
    private MainWindow? _mainWindow;

    public TreeView TreeAppNav => treeAppNav;
    public Grid GridResourceEditor => gridResourceEditor;
    public Button BtnRefreshResources => btnRefreshResources;
    public ListBox LstApplicationResources => lstApplicationResources;
    public TextBox TxtResourceKey => txtResourceKey;
    public TextBox TxtResourceValue => txtResourceValue;
    public Button BtnSaveResource => btnSaveResource;
    public Button BtnAddResource => btnAddResource;

    public ApplicationView()
    {
        InitializeComponent();
    }

    public void Initialize(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    private async void BtnDeleteResource_Click(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null) return;
        var btn = sender as Button;
        string key = btn?.Tag as string ?? "";
        if (string.IsNullOrEmpty(key)) return;

        try
        {
            var p = new System.Text.Json.Nodes.JsonObject { ["key"] = key };
            await _mainWindow.SendCommandAsync("Application.deleteResource", p);
            _mainWindow.RefreshResources();
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Error deleting resource: {ex.Message}");
        }
    }
}
