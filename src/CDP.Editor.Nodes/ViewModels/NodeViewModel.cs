#nullable enable

using System;

namespace CDP.Editor.Nodes.ViewModels;

public class NodeViewModel : NodeEditorViewModelBase
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "";
    private double _x;
    private double _y;
    private double _width = 160;
    private double _height = 100;
    private bool _isSelected;
    private object? _content;
    private bool _isConnectionTarget;
    private Avalonia.Media.IBrush? _borderBrush = Avalonia.Media.Brush.Parse("#3c4043");
    private Avalonia.Media.IBrush? _background = Avalonia.Media.Brush.Parse("#292a2d");
    private Avalonia.Media.IBrush? _titleBackground = Avalonia.Media.Brush.Parse("#35363a");

    public string Id
    {
        get => _id;
        set => RaiseAndSetIfChanged(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public double X
    {
        get => _x;
        set => RaiseAndSetIfChanged(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => RaiseAndSetIfChanged(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => RaiseAndSetIfChanged(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => RaiseAndSetIfChanged(ref _height, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public object? Content
    {
        get => _content;
        set => RaiseAndSetIfChanged(ref _content, value);
    }

    public Avalonia.Media.IBrush? BorderBrush
    {
        get => _borderBrush;
        set => RaiseAndSetIfChanged(ref _borderBrush, value);
    }

    public Avalonia.Media.IBrush? Background
    {
        get => _background;
        set => RaiseAndSetIfChanged(ref _background, value);
    }

    public Avalonia.Media.IBrush? TitleBackground
    {
        get => _titleBackground;
        set => RaiseAndSetIfChanged(ref _titleBackground, value);
    }

    public bool IsConnectionTarget
    {
        get => _isConnectionTarget;
        set => RaiseAndSetIfChanged(ref _isConnectionTarget, value);
    }
}
