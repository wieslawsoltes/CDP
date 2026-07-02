---
title: Troubleshooting
---

# Troubleshooting

Common issues and solutions when working with the CDP Avalonia integration, Inspector application, and OS Automation.

## Connection Issues

### Cannot connect to CDP server

**Symptom:** `curl http://127.0.0.1:9222/json` returns "Connection refused"

**Solutions:**

1. **Verify the app is running with CDP enabled:**
   ```csharp
   CdpServer.Start(9222);
   ```

2. **Check if the port is in use:**
   ```bash
   lsof -iTCP:9222 -sTCP:LISTEN -nP
   ```

3. **Try a different port:**
   ```csharp
   CdpServer.Start(9333);
   ```

4. **Check firewall rules** (Windows):
   ```powershell
   netsh advfirewall firewall show rule name=all | findstr 9222
   ```

### WebSocket connection drops immediately

**Symptom:** WebSocket connects but disconnects within milliseconds

**Solutions:**

- Ensure only one client connects to the same WebSocket endpoint at a time
- Check that the target window hasn't been closed or disposed
- Verify the target ID in the WebSocket URL matches a valid target

### Inspector cannot find targets

**Symptom:** Inspector shows no targets after clicking "Refresh Targets"

**Solutions:**

1. Verify the target application's CDP server is running
2. Check the target URL in the Inspector's connection settings
3. Try `curl http://127.0.0.1:9222/json` manually to confirm the endpoint responds

## DOM Issues

### querySelector returns nodeId 0

**Symptom:** `DOM.querySelector` returns `{"nodeId": 0}` for a selector you expect to match

**Solutions:**

- Call `DOM.getDocument` first to initialize the document tree
- Verify the control has a `Name` attribute set in XAML
- Check selector syntax — `#` prefix is for `Control.Name`, not class names
- Ensure the control is in the visual tree (not collapsed or removed)

### DOM tree is stale or incomplete

**Symptom:** Queried elements don't match the current visual state

**Solutions:**

- Call `DOM.getDocument` again to refresh the tree
- The CDP server captures a snapshot — subsequent visual tree changes require a new `DOM.getDocument` call
- For dynamic content, add a small delay after UI updates before querying

## Runtime Evaluation Issues

### "Cannot find type" errors

**Symptom:** `Runtime.evaluate` fails with type resolution errors

**Solutions:**

- Cast `Window.DataContext` to the concrete type:
  ```csharp
  ((MyApp.ViewModels.MainViewModel)Window.DataContext).PropertyName
  ```
- Ensure all referenced assemblies are loaded
- Use fully qualified type names for types outside the default namespaces

### Lambda expressions fail with dynamic

**Symptom:** LINQ with lambdas fails when used with dynamic types

**Solution:** Cast to concrete types before using LINQ:
```csharp
// ❌ Fails
Window.DataContext.Items.Where(x => x.IsSelected)

// ✅ Works
((MyApp.ViewModels.MainViewModel)Window.DataContext).Items.Where(x => x.IsSelected)
```

### "document is not defined"

**Symptom:** `document.querySelector(...)` fails

**Solution:** The `document` facade is a C# helper object, not JavaScript. It requires `Runtime.enable` to be called first and uses C# string syntax:
```json
{
  "method": "Runtime.evaluate",
  "params": {"expression": "document.querySelector(\"#myControl\").id"}
}
```

## Recorder Issues

### Recording produces no steps

**Symptom:** Clicking "Record" and interacting with the app produces no step entries

**Solutions:**

1. Ensure the Inspector is connected to the target app (check `Connection.IsConnected`)
2. Verify the Recorder tab is active
3. Check that inspect mode is not active (inspect mode suppresses recording)
4. Look for errors in the Inspector's console panel

### Selectors are fragile (type paths instead of #names)

**Symptom:** Recorded selectors look like `Window > Grid > StackPanel > Button`

**Solution:** Add `Name` attributes to interactive controls:
```xml
<Button Name="btnSubmit" Content="Submit" />
```

### Drag actions not detected

**Symptom:** Dragging records as a click instead of a drag

**Solution:** The recorder requires 10+ pixels of mouse movement to register a drag. Ensure the drag distance exceeds this threshold.

## Test Studio Issues

### Steps fail with "Element not found"

**Symptom:** Test Studio step shows "Failed" with selector not matching

**Solutions:**

- Verify the selector matches a visible control: use `DOM.querySelector` manually
- Add `waitForSelector` steps before interactions on dynamically-loaded content
- Check for timing issues — add `delay` steps between rapid interactions

### YAML parsing fails

**Symptom:** "Apply YAML" button produces errors

**Solutions:**

- Validate YAML syntax (correct indentation, no tabs)
- Ensure action names match supported types (case-sensitive)
- Check that required fields (`action`) are present for each step

### Reports are empty or missing

**Symptom:** After execution, no HTML/PDF report is generated

**Solutions:**

- Verify "Generate Reports" checkbox is enabled
- Check that the output directory exists and is writable
- Ensure the test completed (wasn't just stopped mid-execution)

## OS Automation Issues

### macOS: Empty accessibility tree

See [Permissions and Setup](/articles/permissions-setup) for macOS Accessibility permission requirements.

### Windows: Cannot automate elevated apps

Run the Inspector as Administrator:
```powershell
Start-Process dotnet -ArgumentList "run --project CdpInspectorApp" -Verb RunAs
```

## Performance Issues

### High CPU during screencast

**Symptom:** CPU usage spikes when preview or screencast is active

**Solutions:**

- Reduce screencast frame rate
- Use tiled screencast for large windows
- Disable screencast when not needed

### Slow DOM queries

**Symptom:** `DOM.querySelector` takes several seconds

**Solutions:**

- Use specific selectors (`#name`) instead of complex descendant selectors
- Reduce tree depth by querying within a known subtree
- Cache node IDs for repeated queries within the same document snapshot

## Build Issues

### XAML parsing errors

**Symptom:** Build fails with XAML resource or binding errors

**Solutions:**

- Run layout tests: `dotnet test tests/Avalonia.Diagnostics.Cdp.Tests/ViewsLayoutTests.cs`
- Verify all `StaticResource` keys exist in `Styles.axaml`
- Check `x:DataType` annotations match view model types

### Trimming warnings

**Symptom:** AOT or trimmed publish produces warnings

**Solutions:**

- Avoid `dynamic` keyword (project rule)
- Avoid `ReflectionBinding` — use compiled bindings only
- Ensure all types used in `Runtime.evaluate` are preserved

## Next Steps

- [Build, Test, and Release](/articles/build-test-release) — Development workflow
- [Architecture](/articles/architecture) — System architecture
- [Permissions and Setup](/articles/permissions-setup) — Platform permissions
