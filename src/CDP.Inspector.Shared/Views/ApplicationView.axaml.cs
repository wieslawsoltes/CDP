using Avalonia.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ApplicationView : UserControl
{
    public ApplicationView()
    {
        InitializeComponent();
    }
}
