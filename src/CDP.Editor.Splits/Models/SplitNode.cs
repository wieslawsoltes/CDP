using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Media;

namespace CDP.Editor.Splits.Models;

public abstract class SplitNode : INotifyPropertyChanged
{
    private SplitNode? _parent;

    public SplitNode? Parent
    {
        get => _parent;
        set => SetProperty(ref _parent, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class BoxTabNode : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _iconKey = string.Empty;
    private string _selectedViewName = string.Empty;
    private object? _content;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string IconKey
    {
        get => _iconKey;
        set => SetProperty(ref _iconKey, value);
    }

    public string SelectedViewName
    {
        get => _selectedViewName;
        set => SetProperty(ref _selectedViewName, value);
    }

    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public class BoxNode : SplitNode
{
    private readonly ObservableCollection<BoxTabNode> _tabs = new();
    private BoxTabNode? _activeTab;
    private bool _isSelected;
    private string? _backgroundTint;

    public ObservableCollection<BoxTabNode> Tabs => _tabs;

    public BoxNode()
    {
    }

    public BoxNode(string viewName, string title, string iconKey)
    {
        AddTab(title, iconKey, viewName);
    }

    public BoxTabNode AddTab(string title, string iconKey, string viewName)
    {
        var tab = new BoxTabNode
        {
            Title = title,
            IconKey = iconKey,
            SelectedViewName = viewName
        };
        Tabs.Add(tab);
        ActiveTab ??= tab;
        return tab;
    }

    public BoxTabNode? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(IconKey));
                OnPropertyChanged(nameof(SelectedViewName));
                OnPropertyChanged(nameof(Content));
            }
        }
    }

    public string Title
    {
        get => ActiveTab?.Title ?? "Empty Pane";
        set
        {
            if (ActiveTab != null)
            {
                ActiveTab.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string IconKey
    {
        get => ActiveTab?.IconKey ?? "DocumentIcon";
        set
        {
            if (ActiveTab != null)
            {
                ActiveTab.IconKey = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedViewName
    {
        get => ActiveTab?.SelectedViewName ?? string.Empty;
        set
        {
            if (ActiveTab != null)
            {
                ActiveTab.SelectedViewName = value;
                OnPropertyChanged();
            }
        }
    }

    public object? Content
    {
        get => ActiveTab?.Content;
        set
        {
            if (ActiveTab != null)
            {
                ActiveTab.Content = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string? BackgroundTint
    {
        get => _backgroundTint;
        set => SetProperty(ref _backgroundTint, value);
    }
}

public class SplitContainerNode : SplitNode
{
    private Orientation _orientation;
    private SplitNode _child1;
    private SplitNode _child2;
    private double _splitterRatio = 0.5;

    public SplitContainerNode(Orientation orientation, SplitNode child1, SplitNode child2)
    {
        _orientation = orientation;
        _child1 = child1;
        _child2 = child2;
        _child1.Parent = this;
        _child2.Parent = this;
    }

    public Orientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value);
    }

    public SplitNode Child1
    {
        get => _child1;
        set
        {
            if (SetProperty(ref _child1, value))
            {
                if (value != null) value.Parent = this;
            }
        }
    }

    public SplitNode Child2
    {
        get => _child2;
        set
        {
            if (SetProperty(ref _child2, value))
            {
                if (value != null) value.Parent = this;
            }
        }
    }

    public double SplitterRatio
    {
        get => _splitterRatio;
        set => SetProperty(ref _splitterRatio, value);
    }
}
