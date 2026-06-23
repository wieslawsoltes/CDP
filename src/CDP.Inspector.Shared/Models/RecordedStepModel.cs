using System;
using System.Collections.Generic;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Models;

public class RecordedStepModel : ViewModelBase
{
    private string _type = "";
    private string _selector = "";
    private string _value = "";
    private double _offsetX;
    private double _offsetY;
    private double _width;
    private double _height;
    private string _url = "";
    private string _key = "";
    private string _button = "left";
    private int _clickCount = 1;
    private int _modifiers;
    private string _targetSelector = "";
    private double _targetOffsetX;
    private double _targetOffsetY;

    public string Type
    {
        get => _type;
        set
        {
            if (RaiseAndSetIfChanged(ref _type, value))
            {
                OnPropertyChanged(nameof(SelectorDisplay));
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string Selector
    {
        get => _selector;
        set
        {
            if (RaiseAndSetIfChanged(ref _selector, value))
            {
                OnPropertyChanged(nameof(SelectorDisplay));
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (RaiseAndSetIfChanged(ref _value, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public double OffsetX
    {
        get => _offsetX;
        set
        {
            if (RaiseAndSetIfChanged(ref _offsetX, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public double OffsetY
    {
        get => _offsetY;
        set
        {
            if (RaiseAndSetIfChanged(ref _offsetY, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            if (RaiseAndSetIfChanged(ref _width, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (RaiseAndSetIfChanged(ref _height, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (RaiseAndSetIfChanged(ref _url, value))
            {
                OnPropertyChanged(nameof(SelectorDisplay));
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string Key
    {
        get => _key;
        set
        {
            if (RaiseAndSetIfChanged(ref _key, value))
            {
                OnPropertyChanged(nameof(SelectorDisplay));
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string Button
    {
        get => _button;
        set
        {
            if (RaiseAndSetIfChanged(ref _button, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public int ClickCount
    {
        get => _clickCount;
        set
        {
            if (RaiseAndSetIfChanged(ref _clickCount, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public int Modifiers
    {
        get => _modifiers;
        set
        {
            if (RaiseAndSetIfChanged(ref _modifiers, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string TargetSelector
    {
        get => _targetSelector;
        set
        {
            if (RaiseAndSetIfChanged(ref _targetSelector, value))
            {
                OnPropertyChanged(nameof(SelectorDisplay));
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public double TargetOffsetX
    {
        get => _targetOffsetX;
        set
        {
            if (RaiseAndSetIfChanged(ref _targetOffsetX, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public double TargetOffsetY
    {
        get => _targetOffsetY;
        set
        {
            if (RaiseAndSetIfChanged(ref _targetOffsetY, value))
            {
                OnPropertyChanged(nameof(DetailDisplay));
            }
        }
    }

    public string SelectorDisplay
    {
        get
        {
            if (Type == "setViewport") return "Viewport";
            if (Type == "navigate") return "Navigation";
            if (Type == "keydown") return "Keyboard";
            if (Type == "dragAndDrop") return $"Drag: {Selector} -> {TargetSelector}";
            if (Type == "scroll") return $"Scroll: {Selector}";
            return string.IsNullOrEmpty(Selector) ? "Window" : Selector;
        }
    }

    public string DetailDisplay
    {
        get
        {
            if (Type == "click")
            {
                var details = $"Coordinates: x={OffsetX:0.0}, y={OffsetY:0.0}";
                if (Button != "left" || ClickCount > 1) details += $" | Button: {Button} | Clicks: {ClickCount}";
                if (Modifiers > 0) details += $" | Modifiers: {GetModifiersString(Modifiers)}";
                return details;
            }
            if (Type == "change") return $"Value: \"{Value}\"";
            if (Type == "setViewport") return $"Dimensions: {Width}x{Height}";
            if (Type == "navigate") return $"Url: \"{Url}\"";
            if (Type == "keydown")
            {
                var details = $"Key: \"{Key}\"";
                if (Modifiers > 0) details += $" | Modifiers: {GetModifiersString(Modifiers)}";
                return details;
            }
            if (Type == "dragAndDrop")
            {
                var details = $"From: x={OffsetX:0.0}, y={OffsetY:0.0} | To: x={TargetOffsetX:0.0}, y={TargetOffsetY:0.0}";
                if (Modifiers > 0) details += $" | Modifiers: {GetModifiersString(Modifiers)}";
                return details;
            }
            if (Type == "scroll")
            {
                return $"DeltaX: {OffsetX:0.0} | DeltaY: {OffsetY:0.0}";
            }
            return "";
        }
    }

    private static string GetModifiersString(int modifiers)
    {
        var list = new List<string>();
        if ((modifiers & 1) != 0) list.Add("Alt");
        if ((modifiers & 2) != 0) list.Add("Ctrl");
        if ((modifiers & 4) != 0) list.Add("Shift");
        if ((modifiers & 8) != 0) list.Add("Meta");
        return string.Join("+", list);
    }
}
