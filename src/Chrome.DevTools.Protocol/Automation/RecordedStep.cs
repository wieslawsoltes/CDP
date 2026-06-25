using System;

namespace Chrome.DevTools.Protocol;

public class RecordedStep
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
    public int Modifiers { get; set; }
    public string TargetSelector { get; set; } = "";
    public double TargetOffsetX { get; set; }
    public double TargetOffsetY { get; set; }
}
