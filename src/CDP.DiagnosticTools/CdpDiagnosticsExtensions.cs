using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Diagnostics.Cdp;
using CdpServer = Avalonia.Diagnostics.Cdp.CdpServer;

namespace Avalonia;

public static class CdpDiagnosticsExtensions
{
    private static CdpInspectorWindow? _activeInspector;

    public static void AttachCdpInspector(this Window window, int port = 9222)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));

        // Start the server if not already running
        CdpServer.Start(port);

        // Register F12 key handler
        window.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.F12)
            {
                ToggleInspector(window, CdpServer.Port);
                e.Handled = true;
            }
        };
    }

    private static void ToggleInspector(Window targetWindow, int port)
    {
        if (_activeInspector != null)
        {
            if (_activeInspector.IsVisible)
            {
                _activeInspector.Activate();
                return;
            }
            else
            {
                _activeInspector = null;
            }
        }

        _activeInspector = new CdpInspectorWindow(targetWindow, port);
        _activeInspector.Closed += (s, e) => _activeInspector = null;
        _activeInspector.Show();
    }
}
