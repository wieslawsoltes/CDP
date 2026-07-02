---
title: Sources Panel
description: Technical guide to workspace directory tree browsing, TextMate code editing, file saving, active breakpoints, and stepping call stacks in the CDP Inspector for Avalonia.
---

# Sources Panel

The **Sources Panel** is the primary workspace file browser and debugger interface of the CDP Inspector. It allows developers to explore the project directory tree, view source code files with full syntax highlighting, edit contents directly, and manage active execution breakpoints.

---

## 1. Workspace Directory Navigation

The left pane of the Sources Panel hosts the **Files** and **Search** tab control, providing rapid navigation over project resources.

### Workspace Navigator (`treeWorkspaceFiles`)
The directory structure is displayed using a hierarchical `DataGrid` model (`HierarchicalWorkspaceFiles`):
- **Deferred Expansion**: Folder nodes can be expanded to dynamically query and load children, making it efficient for large project workspaces.
- **Node Selection**: Double-clicking or selecting a file node immediately reads the file path, fetching its text content and loading it into the center code editor panel.
- **Directory Resolution**: Folders are represented with directory node items, while file leaves are labeled with their absolute or relative extensions (e.g., `.cs`, `.axaml`, `.xaml`, `.json`, `.md`).

### Search in Workspace (`dgSearchResults`)
The **Search** tab allows performing full-text content searches across all directory files:
- **Case-Sensitive Filter**: Checking `SearchCaseSensitive` toggles strict character matching.
- **Search Results Grid**: Displays matches in a grid containing columns for:
  - **File**: Path of the matching file.
  - **Line**: Line number of the matching text.
  - **Content**: Horizontal preview of the line containing the matched query.
- **Navigation Shortcuts**: Double-clicking a search result entry in `dgSearchResults` instantly opens that file in the central editor pane and scrolls to the matching line.

---

## 2. Code Viewer and TextMate-Powered Editing

The center pane displays the selected file name in `lblSourceFileName` and loads the contents inside a high-fidelity editor control.

### Minimap Code Editor (`MinimapTextEditor`)
The editor features the `MinimapTextEditor` control, which integrates:
- **TextMate Highlighting**: Dynamically styles keywords, comments, strings, and types for multiple languages (specifically C# and XAML/XML).
- **Line Numbers**: Left gutter displaying line indexes, which act as targets for breakpoint toggles.
- **Interactive Minimap**: A right-side visual sidebar representing a zoomed-out view of the file. Clicking or dragging on the minimap scrolls long files.
- **Read-Only Toggle**: By default, compiled files are read-only, but source code items located inside editable workspaces can be toggled to active editing mode.

### File Contents Saving
When modifications are made to a file, the editor tracks its dirty status:
- **Save Button**: The `btnSaveFile` button becomes enabled (`IsFileSelected == true`). Clicking it calls `SaveFileCommand`.
- **Keyboard Shortcut**: Pressing `Ctrl+S` triggers file saving.
- **Server Sync**: The inspector calls the `Save` command over the WebSocket. The backend saves the updated text buffer directly to the local disk workspace, updating compiler files in real-time.

---

## 3. Interactive Debugger controls

The right sidebar is dedicated to execution control and pausing states during code debugging.

### Stepping Control Toolbar
When execution hits a breakpoint on the target application, the target pauses and the inspector toolbar buttons are activated:
- **Resume (Play)**: Sends `Debugger.resume` to continue running the application until the next breakpoint or exception.
- **Step Over**: Steps past the current line of code, staying within the active function block.
- **Step Into**: Steps inside the function call present on the active line.
- **Step Out**: Executes the remainder of the current function and pauses immediately upon returning to the calling block.

---

## 4. Call Stack and Scope Variables Inspection

### Call Stack List
When paused, the **Call Stack** list displays the current sequence of active thread frames:
- **Frame Headers**: Displays the namespace, class, method name, source file basename, and line number (e.g. `ClickEventHandler (MainWindow.axaml.cs:42)`).
- **Context Jumping**: Selecting a parent frame in the list loads its source file in the center editor and points to the execution line.

### Scope Variables Inspector (`dgScopeVariables`)
Exposes all variables local to the active stack frame:
- **Name and Value**: A key-value grid lists parameters, variables, and fields.
- **Behind the Scenes**: When execution pauses, the CDP client extracts the stack frame's `scopeChain`. It queries the `Runtime.getProperties` method using the scope's `objectId`, populating local variable values on the fly.

---

## 5. Breakpoints Management

### Breakpoints List
The **Breakpoints** section lists all active toggles set in the workspace. Selecting a breakpoint highlights its line in the editor.

### Toggling Breakpoints
Breakpoints can be set in two ways:
1. Double-clicking the line number gutter on `MinimapTextEditor`.
2. Placing the text cursor on a line and clicking the **Toggle Breakpoint at Caret** button (`btnToggleBreakpoint`).

### Conditional Breakpoints
Before toggling a breakpoint, developers can enter a C# conditional code snippet in `txtBreakpointCondition` (e.g., `index > 10` or `user == "admin"`):
- The condition is saved along with the breakpoint file path and line number.
- When the target application encounters the line, it evaluates the condition locally. It will pause execution only if the conditional script evaluates to `true`, preventing unnecessary stops in loops.

---

## 6. CDP Backend Architecture Mappings

The Sources panel utilizes the following Chrome DevTools Protocol methods:

### Workspace Registration
```json
{
  "id": 1,
  "method": "Debugger.enable"
}
```

### Loading Directory Structure
```json
{
  "id": 2,
  "method": "Sources.getWorkspaceFiles"
}
```

### Setting Breakpoints
```json
{
  "id": 3,
  "method": "Debugger.setBreakpointByUrl",
  "params": {
    "lineNumber": 42,
    "url": "file:///path/to/MainWindow.axaml.cs",
    "columnNumber": 0,
    "condition": "count == 5"
  }
}
```

### Resuming Code
```json
{
  "id": 4,
  "method": "Debugger.resume"
}
```

This structural architecture ensures that Avalonia desktop application debugging mirrors standard modern developer workspaces, providing C# stepping, workspace search, and source control synchronization over a simple CDP interface.
