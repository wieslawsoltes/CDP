using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;

namespace CDP.Automation.Headless;

public static class CdpHeadlessHelper
{
    public static void ClickControl(this Window window, Control control, MouseButton button, RawInputModifiers modifiers)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? new Point();
        window.MouseDown(point, button, modifiers);
        window.MouseUp(point, button, modifiers);
    }

    public static void DragAndDrop(this Window window, Control source, Control target)
    {
        var startPoint = source.TranslatePoint(new Point(source.Bounds.Width / 2, source.Bounds.Height / 2), window) ?? new Point();
        var endPoint = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? new Point();
        window.MouseMove(startPoint);
        window.MouseDown(startPoint, MouseButton.Left);
        window.MouseMove(endPoint);
        window.MouseUp(endPoint, MouseButton.Left);
    }

    public static void InputText(this Window window, Control control, string text)
    {
        control.Focus();
        window.KeyTextInput(text);
    }

    public static void ClearControl(this Window window, Control control)
    {
        control.Focus();
        if (control is TextBox textBox)
        {
            textBox.Text = "";
        }
        else
        {
            var prop = control.GetType().GetProperty("Text", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(control, "");
            }
        }
    }

    public static async Task LongPressControlAsync(this Window window, Control control, int delayMs = 1000)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? new Point();
        window.MouseDown(point, MouseButton.Left);
        await Task.Delay(delayMs);
        window.MouseUp(point, MouseButton.Left);
    }
}
