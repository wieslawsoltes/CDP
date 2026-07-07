using Avalonia.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TreeView and DataGrid are not trim-safe")]
public partial class MvvmView : UserControl
{
    public MvvmView()
    {
        InitializeComponent();
    }
}
