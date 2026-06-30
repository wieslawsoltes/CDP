using System;
using System.Collections.Generic;

namespace CDP.Automation.OS;

public interface IOsAutomation
{
    bool MovePhysicalCursor { get; set; }
    IReadOnlyList<OSWindow> GetWindows();
    OSNode? GetElementTree(string windowId);
    void SimulateClick(string windowId, double x, double y);
    void SimulateMouseMove(string windowId, double x, double y);
    void SimulateMouseDown(string windowId, double x, double y, string button);
    void SimulateMouseUp(string windowId, double x, double y, string button);
    void SimulateKeyPress(string windowId, string key);
    void SimulateTypeText(string windowId, string text);
    byte[] CaptureWindow(string windowId);
    OSNode? GetFocusedElement(string windowId);
    bool HasScreenCapturePermission();
    void StartInputCapture(string windowId, Action<double, double, string> onClick);
    void StopInputCapture();
}
