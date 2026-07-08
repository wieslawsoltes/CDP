---
title: Uno Platform CDP Support
---

# Uno Platform CDP Support

This library provides full **Chrome DevTools Protocol (CDP)** diagnostics server support for **Uno Platform** cross-platform applications.

Because Uno Platform mirrors the WinUI API structure under the `Microsoft.UI.Xaml` namespace, we share a single codebase for both WinUI and Uno. This enables the same inspection, E2E testing, and input injection capabilities cross-platform on macOS, Linux, and Windows.

---

## 1. Getting Started

### Add Package Reference
Install the `Chrome.DevTools.Uno` package into your Uno Platform application project (typically targeting your Skia desktop head or cross-platform targets):

```bash
dotnet add package Chrome.DevTools.Uno --prerelease
```

---

## 2. Server Initialization

Initialize and start the CDP server in your Uno Platform application startup handler (typically inside `App.xaml.cs` or `Program.cs`):

```csharp
using System;
using Microsoft.UI.Xaml;
using WinUI.Diagnostics.Cdp;

namespace MyUnoApp;

public static class Program
{
    public static void Main(string[] args)
    {
        int port = 9225; // Standard Uno CDP port

        // 1. Start the Uno Platform CDP diagnostics server
        CdpServer.EnsureInitialized();
        CdpServer.Start(port);

        Console.WriteLine($"Uno Application listening on CDP port: {port}");

        // 2. Launch the Application class
        Application.Start(_ => new App());
    }
}
```

Once your application's Window is instantiated, register it with the server as a debug target:

```csharp
// Register the active window as a debug target
CdpServer.GetOrCreateTarget(window);
```

---

## 3. Headless and CI Testing

Uno Platform applications are frequently run in continuous integration (CI) pipelines or headless docker environments. To support testing without a physical graphical user interface:

- The server supports registering window content layouts without native display heads.
- If native window activation fails, catch the window initialization exception and register a virtual window control layout:

```csharp
try
{
    Application.Start(_ => new App());
}
catch (Exception ex)
{
    Console.WriteLine($"Running application in headless mode: {ex.Message}");
    
    // Create a virtual window with a mock control hierarchy
    var window = new Window();
    var stackPanel = new StackPanel { Spacing = 10 };
    stackPanel.Children.Add(new TextBlock { Name = "txtTitle", Text = "Headless Test Page" });
    stackPanel.Children.Add(new Button { Name = "btnSubmit", Content = "Submit" });
    window.Content = stackPanel;
    
    // Register the headless window target
    CdpServer.GetOrCreateTarget(window);
}
```

This allows Playwright, Puppeteer, or Selenium tests to execute queries (`DOM.querySelector`), inspect layouts, and click elements successfully even when running headlessly in Linux/macOS Docker containers.
