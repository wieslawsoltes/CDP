---
title: Permissions and Setup
---

# Permissions and Setup

OS Automation requires specific system permissions to access accessibility trees, capture screenshots, and inject input events. This guide covers the setup process for each supported platform.

## macOS Permissions

macOS requires two explicit user grants for OS Automation to function:

### Accessibility Permission

The Accessibility permission allows the application to read the UI hierarchy of other applications through the AXUIElement API.

**Grant via System Settings:**

1. Open **System Settings** → **Privacy & Security** → **Accessibility**
2. Click the **+** button
3. Navigate to and select the Inspector application (or your .NET host process)
4. Toggle the switch to **ON**

**Verify programmatically:**

```csharp
[LibraryImport("ApplicationServices.framework/ApplicationServices")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool AXIsProcessTrusted();

bool hasAccess = AXIsProcessTrusted();
```

**Request with prompt dialog:**

```csharp
[LibraryImport("ApplicationServices.framework/ApplicationServices")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool AXIsProcessTrustedWithOptions(nint options);
```

Pass a dictionary with `kAXTrustedCheckOptionPrompt = true` to show the system permission dialog.

:::warning
If the Accessibility permission is not granted, `DOM.getDocument` will return an empty tree and all element queries will fail silently. The Inspector will display a permission warning in this state.
:::

### Screen Recording Permission

The Screen Recording permission allows capturing screenshots of other applications' windows.

**Grant via System Settings:**

1. Open **System Settings** → **Privacy & Security** → **Screen Recording**
2. Click the **+** button
3. Select the Inspector application
4. Toggle the switch to **ON**
5. Restart the application (macOS requires a restart after granting this permission)

**Verify programmatically:**

```csharp
// Attempt a test capture - returns null/empty if not permitted
var testImage = CGWindowListCreateImage(
    CGRect.Null,
    CGWindowListOption.OnScreenOnly,
    kCGNullWindowID,
    CGWindowImageOption.Default);

bool hasScreenCapture = testImage != IntPtr.Zero;
```

:::tip
If `Page.captureScreenshot` returns empty or black images, the Screen Recording permission is likely missing. Check System Settings and restart the application after granting.
:::

### Input Monitoring Permission

For keyboard event injection, macOS may additionally require the **Input Monitoring** permission:

1. Open **System Settings** → **Privacy & Security** → **Input Monitoring**
2. Add and enable the Inspector application

## Windows Permissions

### Standard User

Most OS Automation features work under a standard user account:
- UI Automation tree traversal works for same-integrity-level processes
- `SendInput` works for non-elevated windows
- Window screenshots work for visible windows

### Elevated Applications (UAC)

To automate elevated (Run as Administrator) applications, the Inspector itself must also run elevated:

```powershell
# Run Inspector as Administrator
Start-Process -FilePath "dotnet" -ArgumentList "run --project CdpInspectorApp" -Verb RunAs
```

:::warning
A non-elevated process cannot read the UI Automation tree of an elevated process. If you see empty trees for certain applications, try running the Inspector elevated.
:::

### UI Access (UIAccess)

For cross-integrity automation without full elevation, enable UIAccess in the application manifest:

```xml
<requestedExecutionLevel level="asInvoker" uiAccess="true" />
```

Requirements for UIAccess:
- The application must be signed with a trusted certificate
- The application must be installed in a secure location (e.g., `Program Files`)
- `User Account Control: Only elevate UIAccess applications that are installed in secure locations` group policy must be enabled

### Windows Firewall

The CDP server's HTTP listener may trigger a Windows Firewall prompt. Allow the connection for:
- Port 9222 (target app CDP server)
- Port 9223 (Inspector CDP server)

## Linux Permissions

### AT-SPI2

On Linux, the accessibility tree is accessed through AT-SPI2 (Assistive Technology Service Provider Interface):

```bash
# Ensure AT-SPI2 is enabled
gsettings set org.gnome.desktop.interface toolkit-accessibility true

# Install AT-SPI2 development packages (Debian/Ubuntu)
sudo apt install at-spi2-core libatspi2.0-dev
```

### D-Bus Session

AT-SPI2 communicates over D-Bus. Ensure the session bus is available:

```bash
# Verify D-Bus session
echo $DBUS_SESSION_BUS_ADDRESS
```

### Wayland

On Wayland compositors, screenshot capture requires the `wlr-screencopy-unstable-v1` protocol or PipeWire portal:

```bash
# Install xdg-desktop-portal for screenshot support
sudo apt install xdg-desktop-portal xdg-desktop-portal-gtk
```

:::info
Input injection on Wayland is restricted by design. Some compositors (e.g., wlroots-based) support `wtype` or `ydotool` for virtual input. On GNOME Wayland, consider running under XWayland for full input injection support.
:::

## Verification Checklist

After configuring permissions, verify the setup:

| Check | Command/Action | Expected Result |
|---|---|---|
| macOS Accessibility | `AXIsProcessTrusted()` | Returns `true` |
| macOS Screen Recording | Capture test screenshot | Non-empty image data |
| Windows UIA | `DOM.getDocument` on target | Non-empty tree |
| Windows elevated | Run Inspector as admin for elevated targets | Tree visible |
| Linux AT-SPI2 | `gdbus call --session -d org.a11y.Bus ...` | Bus responds |

## Troubleshooting

### macOS: "cannot open because it is from an unidentified developer"

```bash
# Remove quarantine attribute
xattr -cr /path/to/Inspector.app
```

### macOS: Permissions granted but not working

After granting permissions in System Settings, restart the application. macOS caches permission state at launch time.

### Windows: Empty automation tree

- Verify the target application is accessible (not minimized or hidden)
- Check that both processes run at the same integrity level
- Try running the Inspector elevated

### Linux: AT-SPI2 not responding

```bash
# Restart AT-SPI2 bus
systemctl --user restart at-spi-dbus-bus.service
```

## Next Steps

- [OS Automation Overview](/articles/os-automation) — Cross-platform architecture
- [macOS Automation](/articles/macos-automation) — macOS backend details
- [Windows Automation](/articles/windows-automation) — Windows backend details
- [Troubleshooting](/articles/troubleshooting) — General troubleshooting guide
