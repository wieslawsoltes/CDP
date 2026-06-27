using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;

namespace XamlPlayground.Editor.Minimap.Inline;

public sealed class EditorViewZone : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private int _lineNumber = 1;
    private EditorInlinePlacement _placement = EditorInlinePlacement.AfterLine;
    private double _height = 180;
    private double _minHeight = 24;
    private bool _isVisible = true;
    private EditorInlineZoneKind _kind;
    private Control? _content;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public int LineNumber
    {
        get => _lineNumber;
        set => SetField(ref _lineNumber, Math.Max(1, value));
    }

    public EditorInlinePlacement Placement
    {
        get => _placement;
        set => SetField(ref _placement, value);
    }

    public double Height
    {
        get => _height;
        set => SetField(ref _height, Math.Max(0, value));
    }

    public double MinHeight
    {
        get => _minHeight;
        set => SetField(ref _minHeight, Math.Max(0, value));
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    public EditorInlineZoneKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public Control? Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class EditorInlineControl : INotifyPropertyChanged
{
    private int _offset;
    private Control? _control;
    private Func<Control>? _controlFactory;
    private bool _isVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Offset
    {
        get => _offset;
        set => SetField(ref _offset, Math.Max(0, value));
    }

    public Control? Control
    {
        get => _control;
        set => SetField(ref _control, value);
    }

    public Func<Control>? ControlFactory
    {
        get => _controlFactory;
        set => SetField(ref _controlFactory, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class EditorCodeAnnotation : INotifyPropertyChanged
{
    private int _lineNumber = 1;
    private string _text = string.Empty;
    private string? _tooltip;
    private double _priority;
    private ICommand? _command;
    private object? _commandParameter;
    private bool _isVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int LineNumber
    {
        get => _lineNumber;
        set => SetField(ref _lineNumber, Math.Max(1, value));
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public string? ToolTip
    {
        get => _tooltip;
        set => SetField(ref _tooltip, value);
    }

    public double Priority
    {
        get => _priority;
        set => SetField(ref _priority, value);
    }

    public ICommand? Command
    {
        get => _command;
        set => SetField(ref _command, value);
    }

    public object? CommandParameter
    {
        get => _commandParameter;
        set => SetField(ref _commandParameter, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class EditorInlineExtensionCollection : ObservableCollection<IEditorInlineExtension>
{
}
