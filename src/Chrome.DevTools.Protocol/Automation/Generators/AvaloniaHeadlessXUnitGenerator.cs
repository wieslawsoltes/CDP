using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chrome.DevTools.Protocol;

public class AvaloniaHeadlessXUnitGenerator : ICodeGenerator
{
    public RecordingFormat Format => RecordingFormat.AvaloniaHeadlessXUnit;
    public string TabHeader => "Avalonia Headless";
    public string TitleText => "Generated Avalonia Headless xUnit Test";
    public string ExportButtonText => "Export Headless Test";

    public string Generate(IEnumerable<RecordedStep> steps, string hostAddress)
    {
        var stepsList = steps.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Avalonia;");
        sb.AppendLine("using Avalonia.Controls;");
        sb.AppendLine("using Avalonia.Diagnostics.Cdp;");
        sb.AppendLine("using Avalonia.Headless;");
        sb.AppendLine("using Avalonia.Headless.XUnit;");
        sb.AppendLine("using Avalonia.Input;");
        sb.AppendLine("using Avalonia.VisualTree;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine("namespace HeadlessRecordedTests");
        sb.AppendLine("{");
        sb.AppendLine("    public class RecordedTests");
        sb.AppendLine("    {");
        sb.AppendLine("        [AvaloniaFact]");
        sb.AppendLine("        public async Task TestRecordedScenario()");
        sb.AppendLine("        {");
        sb.AppendLine("            // Initialize target window");
        sb.AppendLine("            var window = new CdpSampleApp.MainWindow();");
        sb.AppendLine("            window.Show();");
        sb.AppendLine();
        sb.AppendLine("            // Wait for window layout");
        sb.AppendLine("            await Task.Delay(100);");
        sb.AppendLine("            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Input);");
        sb.AppendLine();

        for (int i = 0; i < stepsList.Count; i++)
        {
            var step = stepsList[i];
            sb.AppendLine($"            // Step {i + 1}: {step.Type}");

            if (step.Type == "setViewport")
            {
                sb.AppendLine($"            window.Width = {(int)step.Width};");
                sb.AppendLine($"            window.Height = {(int)step.Height};");
            }
            else if (step.Type == "navigate")
            {
                sb.AppendLine($"            if (window is CdpSampleApp.MainWindow mainWin)");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                mainWin.Navigate(\"{EscapeCSharpString(step.Url)}\");");
                sb.AppendLine($"            }}");
            }
            else if (step.Type == "click")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                string buttonEnum = GetMouseButton(step.Button);
                string modifiersEnum = GetModifiersEnum(step.Modifiers);

                sb.AppendLine($"            var element_{i} = SelectorEngine.QuerySelector(window, \"{selectorEscaped}\") as Control;");
                sb.AppendLine($"            Assert.NotNull(element_{i});");
                if (step.ClickCount > 1)
                {
                    sb.AppendLine($"            for (int c_{i} = 0; c_{i} < {step.ClickCount}; c_{i}++)");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                ClickControl(window, element_{i}, {buttonEnum}, {modifiersEnum});");
                    sb.AppendLine($"            }}");
                }
                else
                {
                    sb.AppendLine($"            ClickControl(window, element_{i}, {buttonEnum}, {modifiersEnum});");
                }
            }
            else if (step.Type == "change")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                string valueEscaped = EscapeCSharpString(step.Value);

                sb.AppendLine($"            var element_{i} = SelectorEngine.QuerySelector(window, \"{selectorEscaped}\") as Control;");
                sb.AppendLine($"            Assert.NotNull(element_{i});");
                sb.AppendLine($"            element_{i}.Focus();");
                sb.AppendLine($"            window.KeyTextInput(\"{valueEscaped}\");");
            }
            else if (step.Type == "keydown")
            {
                string keyEnum = GetKeyEnum(step.Key);
                string modifiersEnum = GetModifiersEnum(step.Modifiers);

                sb.AppendLine($"            window.KeyPress({keyEnum}, {modifiersEnum});");
                sb.AppendLine($"            window.KeyRelease({keyEnum}, {modifiersEnum});");
            }
            else if (step.Type == "dragAndDrop")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                string targetSelectorEscaped = EscapeCSharpString(step.TargetSelector);

                sb.AppendLine($"            var source_{i} = SelectorEngine.QuerySelector(window, \"{selectorEscaped}\") as Control;");
                sb.AppendLine($"            var target_{i} = SelectorEngine.QuerySelector(window, \"{targetSelectorEscaped}\") as Control;");
                sb.AppendLine($"            Assert.NotNull(source_{i});");
                sb.AppendLine($"            Assert.NotNull(target_{i});");
                sb.AppendLine($"            DragAndDrop(window, source_{i}, target_{i});");
            }
            else if (step.Type == "scroll")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);
                sb.AppendLine($"            // Scroll element or page");
                if (!string.IsNullOrEmpty(step.Selector))
                {
                    sb.AppendLine($"            var element_{i} = SelectorEngine.QuerySelector(window, \"{selectorEscaped}\") as Control;");
                    sb.AppendLine($"            if (element_{i} != null)");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                var sv_{i} = element_{i} is ScrollViewer ? (ScrollViewer)element_{i} : element_{i}.FindAncestorOfType<ScrollViewer>();");
                    sb.AppendLine($"                if (sv_{i} != null) sv_{i}.Offset = new Vector(sv_{i}.Offset.X - {step.OffsetX}, sv_{i}.Offset.Y - {step.OffsetY});");
                    sb.AppendLine($"            }}");
                }
            }
            else if (step.Type == "assertVisible")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);

                sb.AppendLine($"            var element_{i} = SelectorEngine.QuerySelector(window, \"{selectorEscaped}\") as Control;");
                sb.AppendLine($"            Assert.NotNull(element_{i});");
                sb.AppendLine($"            Assert.True(element_{i}.IsVisible);");
            }
            else if (step.Type == "assertNotVisible")
            {
                string selectorEscaped = EscapeCSharpString(step.Selector);

                sb.AppendLine($"            var element_{i} = SelectorEngine.QuerySelector(window, \"{selectorEscaped}\") as Control;");
                sb.AppendLine($"            Assert.True(element_{i} == null || !element_{i}.IsVisible);");
            }
            sb.AppendLine();
        }

        sb.AppendLine("            await Task.Delay(50);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static void ClickControl(Window window, Control control, MouseButton button, RawInputModifiers modifiers)");
        sb.AppendLine("        {");
        sb.AppendLine("            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? new Point();");
        sb.AppendLine("            window.MouseDown(point, button, modifiers);");
        sb.AppendLine("            window.MouseUp(point, button, modifiers);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static void DragAndDrop(Window window, Control source, Control target)");
        sb.AppendLine("        {");
        sb.AppendLine("            var startPoint = source.TranslatePoint(new Point(source.Bounds.Width / 2, source.Bounds.Height / 2), window) ?? new Point();");
        sb.AppendLine("            var endPoint = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? new Point();");
        sb.AppendLine("            window.MouseMove(startPoint);");
        sb.AppendLine("            window.MouseDown(startPoint, MouseButton.Left);");
        sb.AppendLine("            window.MouseMove(endPoint);");
        sb.AppendLine("            window.MouseUp(endPoint, MouseButton.Left);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetMouseButton(string button)
    {
        switch (button.ToLower())
        {
            case "right": return "MouseButton.Right";
            case "middle": return "MouseButton.Middle";
            default: return "MouseButton.Left";
        }
    }

    private static string GetModifiersEnum(int modifiers)
    {
        if (modifiers == 0) return "RawInputModifiers.None";
        var list = new List<string>();
        if ((modifiers & 1) != 0) list.Add("RawInputModifiers.Alt");
        if ((modifiers & 2) != 0) list.Add("RawInputModifiers.Control");
        if ((modifiers & 4) != 0) list.Add("RawInputModifiers.Meta");
        if ((modifiers & 8) != 0) list.Add("RawInputModifiers.Shift");
        return string.Join(" | ", list);
    }

    private static readonly Dictionary<string, string> _knownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = "Key.Enter",
        ["Return"] = "Key.Enter",
        ["Tab"] = "Key.Tab",
        ["Escape"] = "Key.Escape",
        ["Backspace"] = "Key.Back",
        ["Back"] = "Key.Back",
        ["Delete"] = "Key.Delete",
        ["Insert"] = "Key.Insert",
        ["ArrowUp"] = "Key.Up",
        ["Up"] = "Key.Up",
        ["ArrowDown"] = "Key.Down",
        ["Down"] = "Key.Down",
        ["ArrowLeft"] = "Key.Left",
        ["Left"] = "Key.Left",
        ["ArrowRight"] = "Key.Right",
        ["Right"] = "Key.Right",
        ["PageUp"] = "Key.PageUp",
        ["PageDown"] = "Key.PageDown",
        ["Home"] = "Key.Home",
        ["End"] = "Key.End",
        ["Space"] = "Key.Space",
        ["CapsLock"] = "Key.CapsLock",
        ["NumLock"] = "Key.NumLock",
        ["Scroll"] = "Key.Scroll",
        ["PrintScreen"] = "Key.PrintScreen",
        ["Pause"] = "Key.Pause",
        ["LWin"] = "Key.LWin",
        ["RWin"] = "Key.RWin",
        ["Apps"] = "Key.Apps",
        ["Sleep"] = "Key.Sleep",
        ["NumPad0"] = "Key.NumPad0",
        ["NumPad1"] = "Key.NumPad1",
        ["NumPad2"] = "Key.NumPad2",
        ["NumPad3"] = "Key.NumPad3",
        ["NumPad4"] = "Key.NumPad4",
        ["NumPad5"] = "Key.NumPad5",
        ["NumPad6"] = "Key.NumPad6",
        ["NumPad7"] = "Key.NumPad7",
        ["NumPad8"] = "Key.NumPad8",
        ["NumPad9"] = "Key.NumPad9",
        ["Multiply"] = "Key.Multiply",
        ["Add"] = "Key.Add",
        ["Separator"] = "Key.Separator",
        ["Subtract"] = "Key.Subtract",
        ["Decimal"] = "Key.Decimal",
        ["Divide"] = "Key.Divide",
        ["LeftShift"] = "Key.LeftShift",
        ["RightShift"] = "Key.RightShift",
        ["LeftCtrl"] = "Key.LeftCtrl",
        ["RightCtrl"] = "Key.RightCtrl",
        ["LeftAlt"] = "Key.LeftAlt",
        ["RightAlt"] = "Key.RightAlt",
        // F keys
        ["F1"] = "Key.F1", ["F2"] = "Key.F2", ["F3"] = "Key.F3", ["F4"] = "Key.F4",
        ["F5"] = "Key.F5", ["F6"] = "Key.F6", ["F7"] = "Key.F7", ["F8"] = "Key.F8",
        ["F9"] = "Key.F9", ["F10"] = "Key.F10", ["F11"] = "Key.F11", ["F12"] = "Key.F12",
        ["F13"] = "Key.F13", ["F14"] = "Key.F14", ["F15"] = "Key.F15", ["F16"] = "Key.F16",
        ["F17"] = "Key.F17", ["F18"] = "Key.F18", ["F19"] = "Key.F19", ["F20"] = "Key.F20",
        ["F21"] = "Key.F21", ["F22"] = "Key.F22", ["F23"] = "Key.F23", ["F24"] = "Key.F24",
    };

    private static string GetKeyEnum(string key)
    {
        if (string.IsNullOrEmpty(key)) return "Key.None";

        if (_knownKeys.TryGetValue(key, out var known))
        {
            return known;
        }

        string cleaned = key;
        if (key.StartsWith("Key", StringComparison.OrdinalIgnoreCase) && key.Length > 3)
        {
            cleaned = key.Substring(3);
        }
        else if (key.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && key.Length > 5)
        {
            cleaned = "D" + key.Substring(5);
        }

        if (cleaned.Length == 1)
        {
            char c = cleaned[0];
            if (char.IsLetter(c))
            {
                return $"Key.{char.ToUpper(c)}";
            }
            if (char.IsDigit(c))
            {
                return $"Key.D{c}";
            }
        }
        else if (cleaned.Length > 1 && cleaned.All(char.IsLetterOrDigit))
        {
            string pascal = char.ToUpper(cleaned[0]) + cleaned.Substring(1);
            return $"Key.{pascal}";
        }

        return "Key.None";
    }

    private static string EscapeCSharpString(string? value)
    {
        if (value == null) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
