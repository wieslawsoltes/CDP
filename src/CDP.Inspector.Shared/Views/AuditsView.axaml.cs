using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class AuditsView : UserControl
{
    public AuditsView()
    {
        InitializeComponent();
    }
}
