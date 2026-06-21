using System;
using System.Collections.Generic;

namespace CdpInspectorApp.Models;

public class RecordedStepModel
{
    public string Type { get; set; } = "";
    public string Selector { get; set; } = "";
    public string Value { get; set; } = "";
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Url { get; set; } = "";
    public string Key { get; set; } = "";
    public string Button { get; set; } = "left";
    public int ClickCount { get; set; } = 1;
    public int Modifiers { get; set; } = 0;
    public string TargetSelector { get; set; } = "";
    public double TargetOffsetX { get; set; }
    public double TargetOffsetY { get; set; }

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
