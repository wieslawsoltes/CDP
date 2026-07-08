---
title: WinUI 3 CDP Support
---

# WinUI 3 CDP Support

This library provides full **Chrome DevTools Protocol (CDP)** diagnostics server support for **WinUI 3** desktop applications. 

With this package embedded, you can inspect your WinUI visual tree, map touch/mouse coordinates, capture screenshots, and query element states using standard browser testing frameworks like Playwright, Puppeteer, or Selenium.

---

## 1. Getting Started

### Add Package Reference
Install the `Chrome.DevTools.WinUI` package into your WinUI 3 application project:

```bash
dotnet add package Chrome.DevTools.WinUI --prerelease
```

---

## 2. Server Initialization

Initialize and start the CDP server, and register your active window in your application startup handler (typically inside `App.xaml.cs` or the main program startup):

```csharp
using System;
using Microsoft.UI.Xaml;
using WinUI.Diagnostics.Cdp;

namespace MyWinUiApp;

public partial class App : Application
{
    private Window? _window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        int port = 9225; // Standard WinUI CDP port

        // 1. Initialize and start the diagnostics server
        CdpServer.EnsureInitialized();
        CdpServer.Start(port);

        Console.WriteLine($"WinUI Application listening on CDP port: {port}");

        // 2. Instantiate your window and register it with the server
        _window = new MainWindow();
        CdpServer.GetOrCreateTarget(_window);

        _window.Activate();
    }
}
```

Stop the server when the application shuts down:

```csharp
// Call this on application shutdown
CdpServer.Stop();
```

---

## 3. Key Architecture & Features

### Transparent Canvas Overlay Injection
Unlike WPF and Avalonia, WinUI 3 does not expose a native `AdornerLayer` for adding arbitrary decorations over controls. 
To support element highlights (padding/margin/content borders) and size tooltips:
- The WinUI CDP library injects a transparent `Canvas` control directly into the window's root layout container.
- When an element is selected, the overlay canvas calculates the control's position in screen space (relative to the window root) and draws colored rectangles representing padding, borders, margins, and tooltips.
- The canvas ignores hit tests (`IsHitTestVisible = false`) so it does not interfere with click simulations.

### Coordinates Mapping
Element coordinates, bounds, and hover inspect modes leverage WinUI's `VisualTreeHelper.FindElementsInHostCoordinates` method to map coordinate spaces accurately.

### Multi-Window Tracking
If your application instantiates multiple `Window` objects, call `CdpServer.GetOrCreateTarget(window)` for each new instance. The CDP server registers them as independent pages, exposing them as distinct debug targets in the targets list.
