# Pre-flight Wait For Debugger

In automated testing environments (such as Appium, Selenium, or playwright/puppeteer script runs), it is often necessary to pause the target application's startup. This allows testing frameworks to hook into the debugger, attach listeners, and preload instrumentation scripts *before* the application constructs its main window or runs its initial logic.

The Avalonia CDP server supports this pattern using the `--wait-for-debugger` command-line argument and standard Chrome DevTools Protocol pausing commands.

---

## 1. Startup Pausing (`--wait-for-debugger`)

When launching an Avalonia application from the command line, you can pass the `--wait-for-debugger` flag:

```shell
dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj -- --wait-for-debugger
```

### The Pausing Mechanism

1.  During startup, the CDP server starts up on port `9222` and checks the command-line arguments. If `--wait-for-debugger` is present, it sets:
    ```csharp
    CdpServer.WaitForDebugger = true;
    ```
2.  The server hooks into the Avalonia Window Opened event (`Window.WindowOpenedEvent.AddClassHandler<Window>`).
3.  When the main window starts opening, `ShouldWaitForDebugger(w)` is checked. Since wait-for-debugger is enabled and the server has not yet resumed, it registers the target as paused:
    ```csharp
    Chrome.DevTools.Protocol.CdpServer.SetTargetWaitingForDebugger(targetId, true);
    ```
4.  To pause execution without blocking network communication or freezing the operating system window manager, the server utilizes a nested **`DispatcherFrame`** loop:
    ```csharp
    var frame = new DispatcherFrame();
    _pausedFrames[targetId] = frame;
    Dispatcher.UIThread.PushFrame(frame);
    ```
    *   `Dispatcher.UIThread.PushFrame` halts the normal execution path of the UI thread.
    *   However, the dispatcher continues processing windows events, UI messages, and CDP WebSocket requests, keeping the application alive and responsive to incoming commands.

---

## 2. Script Preloading (`Page.addScriptToEvaluateOnNewDocument`)

While the application is paused, the test runner can register custom script payloads to run as soon as execution resumes. This is crucial for setting up mocks, registering custom state handlers, or injecting testing hooks.

*   **CDP Method**: `Page.addScriptToEvaluateOnNewDocument`
*   **Parameters**:
    *   `source` (`string`): The script source code to evaluate.
    *   `worldName` (`string`, optional): The execution world name.

The CDP server saves the pre-flight scripts inside the session context:
```csharp
session.ScriptsToEvaluateOnNewDocument[identifier] = source;
```

---

## 3. Resuming Execution (`Runtime.runIfWaitingForDebugger`)

Once the debugger has registered all necessary event listeners and preloaded its startup scripts, it sends the resume command.

*   **CDP Method**: `Runtime.runIfWaitingForDebugger`

### The Resuming Pipeline

Upon receiving this command, the server executes the following sequence:

1.  **Evaluate Preloaded Scripts**: The server iterates over all registered pre-flight scripts in `ScriptsToEvaluateOnNewDocument` and runs them in order within the target context:
    ```csharp
    await EvaluateAsync(session, ScriptPreprocessor.Preprocess(script), inspectedNodeId: 0);
    ```
2.  **Resume target**: The server requests the target manager to exit the pause loop:
    ```csharp
    CdpServer.ResumeTarget(targetId);
    ```
3.  **Exit Dispatcher Frame**: `ResumeTarget` marks the target as active and stops the dispatcher frame:
    ```csharp
    if (_pausedFrames.TryRemove(targetId, out var frame))
    {
        frame.Continue = false;
    }
    ```
    Setting `frame.Continue = false` causes `Dispatcher.UIThread.PushFrame` to exit, allowing the window layout, initialization methods, and main logic of the application to execute normally.
