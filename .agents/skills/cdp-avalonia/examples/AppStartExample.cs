using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;

namespace CdpExamples;

public class AppStartExample
{
    // Option 1: Quick integration using the extension method
    public static void InitializeWithExtension(Window mainWindow)
    {
        // Automatically starts the server on port 9222 and binds F12 to open the Inspector
        mainWindow.AttachCdpInspector(port: 9222);
    }

    // Option 2: Custom programmatic startup & registration
    public static void InitializeCustom(Window mainWindow)
    {
        try
        {
            // 1. Start the HTTP listener and WebSocket dispatch server on port 9222
            CdpServer.Start(port: 9222);
            Console.WriteLine($"CDP Server listening on: http://localhost:9222");

            // 2. Register the main window so that it shows up as an inspectable page target
            string targetId = CdpServer.Register(mainWindow, "MainWindow Target");
            Console.WriteLine($"Registered window '{mainWindow.Title}' with target ID: {targetId}");

            // 3. Optional: Hook exit event to stop the server
            mainWindow.Closed += (s, e) =>
            {
                CdpServer.Stop();
                Console.WriteLine("CDP Server stopped.");
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start CDP Server: {ex.Message}");
        }
    }
}
