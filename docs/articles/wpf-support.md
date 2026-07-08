---
title: WPF CDP Support
---

# WPF CDP Support

This library provides full **Chrome DevTools Protocol (CDP)** diagnostics server support for **Windows Presentation Foundation (WPF)** applications. 

By adding a lightweight, embedded CDP listener, you can query your WPF application's visual tree, inspect control properties, trigger pointer/keyboard simulations, and execute runtime C# scripts using standard browser-automation frameworks like Playwright, Puppeteer, or Selenium.

---

## 1. Getting Started

### Add Package Reference
Install the `Chrome.DevTools.Wpf` package into your WPF executable project:

```bash
dotnet add package Chrome.DevTools.Wpf --prerelease
```

> [!NOTE]
> The WPF implementation compiles the active CDP server implementation only when targeting Windows (`net10.0-windows` or similar). On non-Windows platforms (like macOS or Linux), it compiles a placeholder stub class to keep the code compilation cross-platform safe.

---

## 2. Server Initialization

Initialize and start the CDP server in your WPF application's startup file (typically `App.xaml.cs` by overriding `OnStartup`):

```csharp
using System;
using System.Windows;
using Wpf.Diagnostics.Cdp;

namespace MyWpfApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        int port = 9224; // Standard WPF CDP port
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                int.TryParse(e.Args[i + 1], out port);
            }
        }

        // Initialize and launch the diagnostics server
        CdpServer.EnsureInitialized();
        CdpServer.Start(port);

        Console.WriteLine($"WPF Application listening on CDP port: {port}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop the server on application exit
        CdpServer.Stop();
        base.OnExit(e);
    }
}
```

---

## 3. Key Architecture & Features

### WPF Visual & Logical Tree
WPF uses a dual-tree model. The CDP server traverses:
- **Visual Tree**: Via WPF's `VisualTreeHelper` to map physical coordinates, inspect control layout, and compile high-DPI screenshots.
- **Logical Tree**: Via WPF's `LogicalTreeHelper` to query logical parents/children and resolve bindings, matching element relationships.

### Highlighting via Adorners
Unlike other custom canvas approaches, the WPF integration utilizes the framework's native **Adorner Layer**. When an element is hovered over or selected inside the inspector:
- A `HighlightAdorner` is dynamically attached to the element's parent `AdornerLayer`, rendering colored padding, border, and content boxes matching the standard Chrome DevTools visual box model.
- A `PaintRectsAdorner` flashes colored borders over controls when they trigger a layout update, allowing you to debug rendering performance.

### Selector Support
The selector engine resolves standard browser CSS selectors:
- `#btnSubmit` matches controls where `Name = "btnSubmit"`.
- `Button` matches control types or full-qualified namespace types.
- Attribute queries like `[AutomationProperties.AutomationId="txtInput"]` lookup the WPF `AutomationProperties.GetAutomationId` value.
