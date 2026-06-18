using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;

namespace Avalonia;

public partial class CdpInspectorWindow : Window
{
    public CdpInspectorWindow()
    {
        InitializeComponent();
    }

    public CdpInspectorWindow(Window targetWindow, int port) : this()
    {
        if (targetWindow == null) throw new ArgumentNullException(nameof(targetWindow));

        // Auto-connect to the target window on load
        Dispatcher.UIThread.Post(async () =>
        {
            if (MainViewControl.DataContext is MainWindowViewModel vm)
            {
                try
                {
                    // Register the target window and get its target ID
                    var targetId = CdpServer.Register(targetWindow, targetWindow.Title ?? "Target Window");
                    var host = $"http://127.0.0.1:{port}";
                    var wsUrl = $"ws://127.0.0.1:{port}/devtools/page/{targetId}";

                    var targetItem = new TargetItem(targetWindow.Title ?? "Target Window", wsUrl, targetId);

                    // Set host address on connection view model
                    vm.Connection.HostAddress = host;

                    // Connect using CdpService
                    await vm.CdpService.ConnectAsync(host, targetItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CdpInspectorWindow] Auto-connect failed: {ex.Message}");
                }
            }
        });
    }

    public void LoadScriptContent(string content)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Recorder.LoadScriptContent(content);
        }
    }
}
