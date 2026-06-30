using System;
using System.Collections.Generic;

namespace CDP.Automation.OS;

public interface IOsAutomation
{
    bool MovePhysicalCursor { get; set; }
    bool UsePeerAutomation { get; set; }
    bool UseAccessibilityEvents { get; set; }
    IReadOnlyList<OSWindow> GetWindows();
    OSNode? GetElementTree(string windowId);
    void SimulateClick(string windowId, double x, double y);
    void SimulateMouseMove(string windowId, double x, double y);
    void SimulateMouseDown(string windowId, double x, double y, string button);
    void SimulateMouseUp(string windowId, double x, double y, string button);
    void SimulateMouseWheel(string windowId, double x, double y, double deltaX, double deltaY);
    void SimulateKeyPress(string windowId, string key);
    void SimulateTypeText(string windowId, string text);
    byte[] CaptureWindow(string windowId);
    OSNode? GetFocusedElement(string windowId);
    bool HasScreenCapturePermission();
    bool HasAccessibilityPermission();
    void StartInputCapture(string windowId, Action<double, double, string> onClick, Action<string, string, string?> onAccessibilityEvent);
    void StopInputCapture();
}
