#nullable enable

using System.Collections.ObjectModel;

namespace CDP.Editor.Nodes.ViewModels;

public class GroupNodeViewModel : NodeViewModel
{
    public ObservableCollection<string> ChildNodeIds { get; } = new();

    public GroupNodeViewModel()
    {
        Width = 300;
        Height = 200;
        Background = Avalonia.Media.Brush.Parse("#25272a");
        BorderBrush = Avalonia.Media.Brush.Parse("#4f535c");
        TitleBackground = Avalonia.Media.Brush.Parse("#2e3138");
    }
}
