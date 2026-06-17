using Avalonia.Controls;

namespace CdpInspectorApp.Views;

public partial class SourcesView : UserControl
{
    public TreeView TreeWorkspaceFiles => treeWorkspaceFiles;
    public TextBlock LblSourceFileName => lblSourceFileName;
    public TextBox TxtSourceContent => txtSourceContent;

    public SourcesView()
    {
        InitializeComponent();
    }
}
