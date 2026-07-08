using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class InputDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "dispatchMouseEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    double x = GetDoubleOrDefault(@params["x"], 0);
                    double y = GetDoubleOrDefault(@params["y"], 0);
                    string button = @params["button"]?.GetValue<string>() ?? "none";
                    double deltaX = GetDoubleOrDefault(@params["deltaX"], 0);
                    double deltaY = GetDoubleOrDefault(@params["deltaY"], 0);

                    await DispatchMouseEventAsync(session, type, x, y, button, deltaX, deltaY);
                    return new JsonObject();
                }

            case "dispatchKeyEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    string keyStr = @params["key"]?.GetValue<string>() ?? "";
                    string text = @params["text"]?.GetValue<string>() ?? "";

                    await DispatchKeyEventAsync(session, type, keyStr, text);
                    return new JsonObject();
                }

            case "insertText":
                {
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    await DispatchTextInputAsync(session, text);
                    return new JsonObject();
                }

            case "emulateTouchFromMouseEvent":
            case "synthesizeTapGesture":
            case "synthesizeScrollGesture":
            case "setIgnoreInputEvents":
                return new JsonObject();

            default:
                throw new Exception($"Method Input.{action} is not implemented");
        }
    }

    private static double GetDoubleOrDefault(JsonNode? node, double def)
    {
        if (node == null) return def;
        try { return node.GetValue<double>(); } catch { return def; }
    }

    private static async Task DispatchMouseEventAsync(CdpSession session, string type, double x, double y, string button, double deltaX, double deltaY)
    {
        if (session.Window == null) return;

        var window = session.Window;
        await window.DispatcherQueue.InvokeAsync(() =>
        {
            if (window.Content == null) return;

            var elements = VisualTreeHelper.FindElementsInHostCoordinates(new Point(x, y), window.Content);
            var hit = elements.FirstOrDefault();
            if (hit == null) return;

            if (type == "mousePressed")
            {
                if (hit is Control ctrl)
                {
                    ctrl.Focus(FocusState.Pointer);
                }

                if (hit is Button buttonControl)
                {
                    var peer = new ButtonAutomationPeer(buttonControl);
                    var invokeProvider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProvider?.Invoke();
                }
                else if (hit is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase baseButton)
                {
                    // Generic button invoke fallback
                    var method = baseButton.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    method?.Invoke(baseButton, null);
                }
            }
        });
    }

    private static async Task DispatchKeyEventAsync(CdpSession session, string type, string keyStr, string text)
    {
        if (session.Window == null) return;

        var window = session.Window;
        await window.DispatcherQueue.InvokeAsync(() =>
        {
            if (!string.IsNullOrEmpty(text) && (type == "keyDown" || type == "rawKeyDown"))
            {
                _ = DispatchTextInputAsync(session, text);
            }
        });
    }

    private static async Task DispatchTextInputAsync(CdpSession session, string text)
    {
        if (session.Window == null) return;

        var window = session.Window;
        await window.DispatcherQueue.InvokeAsync(() =>
        {
            if (window.Content == null) return;
            var focused = FocusManager.GetFocusedElement(window.Content.XamlRoot);
            if (focused is TextBox tb)
            {
                int start = tb.SelectionStart;
                string currentText = tb.Text ?? "";
                tb.Text = currentText.Substring(0, start) + text + currentText.Substring(start + tb.SelectionLength);
                tb.SelectionStart = start + text.Length;
                tb.SelectionLength = 0;
            }
        });
    }
}
