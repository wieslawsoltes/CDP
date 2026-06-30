using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;

namespace CDP.Automation.OS;

public sealed class EnrichedOsAutomation : IOsAutomation
{
    private readonly IOsAutomation _underlying;

    public EnrichedOsAutomation(IOsAutomation underlying)
    {
        _underlying = underlying;
    }

    public IReadOnlyList<OSWindow> GetWindows()
    {
        var list = new List<OSWindow>(_underlying.GetWindows());

        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    if (proc.Id <= 0) continue;

                    bool exists = false;
                    foreach (var w in list)
                    {
                        if (w.ProcessId == proc.Id)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        string title = proc.MainWindowTitle;
                        if (string.IsNullOrEmpty(title))
                        {
                            title = $"{proc.ProcessName} (Universal Window)";
                        }

                        string id = proc.MainWindowHandle != IntPtr.Zero ? proc.MainWindowHandle.ToString() : $"{proc.Id}_fallback";

                        list.Add(new OSWindow
                        {
                            Id = id,
                            Title = title,
                            ProcessName = proc.ProcessName,
                            ProcessId = proc.Id,
                            Bounds = new SKRectI(100, 100, 1124, 868)
                        });
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return list;
    }

    public bool MovePhysicalCursor
    {
        get => _underlying.MovePhysicalCursor;
        set => _underlying.MovePhysicalCursor = value;
    }

    public OSNode? GetElementTree(string windowId) => _underlying.GetElementTree(windowId);
    public void SimulateClick(string windowId, double x, double y) => _underlying.SimulateClick(windowId, x, y);
    public void SimulateMouseMove(string windowId, double x, double y) => _underlying.SimulateMouseMove(windowId, x, y);
    public void SimulateMouseDown(string windowId, double x, double y, string button) => _underlying.SimulateMouseDown(windowId, x, y, button);
    public void SimulateMouseUp(string windowId, double x, double y, string button) => _underlying.SimulateMouseUp(windowId, x, y, button);
    public void SimulateKeyPress(string windowId, string key) => _underlying.SimulateKeyPress(windowId, key);
    public void SimulateTypeText(string windowId, string text) => _underlying.SimulateTypeText(windowId, text);
    public byte[] CaptureWindow(string windowId) => _underlying.CaptureWindow(windowId);
    public OSNode? GetFocusedElement(string windowId) => _underlying.GetFocusedElement(windowId);
    public bool HasScreenCapturePermission() => _underlying.HasScreenCapturePermission();
    public void StartInputCapture(string windowId, Action<double, double, string> onClick) => _underlying.StartInputCapture(windowId, onClick);
    public void StopInputCapture() => _underlying.StopInputCapture();
}
