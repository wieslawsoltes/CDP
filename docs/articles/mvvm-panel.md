# MVVM Diagnostics Panel

The `CdpInspectorApp` includes a dedicated diagnostics panel for inspecting and manipulating the Model-View-ViewModel (MVVM) state of a connected target application. 

This panel provides an interactive visual tree representing active ViewModels, a real-time property editor grid, and an execution history log of application commands.

---

## Accessing the Panel

1.  Launch `CdpInspectorApp` and connect to your target application (e.g. `CdpSampleApp` running on port `9222`).
2.  In the right pane tabs, select the **MVVM** tab.
3.  The MVVM Diagnostics panel will display, split into three main layout panes.

---

## Layout and Core Panes

The panel is organized into a toolbar and three main interface panels designed for developers and testing agents:

### 1. The Active ViewModels Tree Pane
Located on the left side of the panel, this pane displays the active hierarchy of ViewModels based on the target application's visual tree.
*   **Node Presentation**: Displays the hosting control class name, its programmatic name (`Name` property in XAML), and the short class name of its `DataContext` ViewModel (e.g. `MainWindowViewModel`).
*   **Selection**: Selecting a node updates the properties grid to show the properties of that specific ViewModel.
*   **Expansion**: Nodes can be collapsed and expanded. The inspector remembers expanded state across tree refreshes.

### 2. The ViewModel Properties Grid Editor
Located on the top-right side, this grid displays all properties exposed by the selected ViewModel.
*   **Columns**:
    *   **Property**: The name of the property.
    *   **Type**: The fully qualified C# data type (e.g. `System.String`, `System.Boolean`).
    *   **Value**: The current serialized value.
*   **Interactive Editing**:
    *   If a property is read-only (`isWritable: false`), the textbox is disabled.
    *   If a property is writable (`isWritable: true`), you can modify the text in the textbox.
    *   Clicking the **Set** button parses your edit and pushes the update to the target application instantly over the CDP link.

### 3. Command Execution History
Located along the bottom of the panel, this pane displays a real-time history of executed commands in the target application (such as `ReactiveCommand` button taps or async operations).
*   **Captured Metadata**:
    *   **Timestamp**: The exact UTC date and time the command completed.
    *   **ViewModel**: The type of the ViewModel containing the command.
    *   **Command Name**: The name of the command property.
    *   **Execution Result**: The serialized output returned by the command execution.

---

## Toolbar Commands

The top toolbar offers quick shortcuts to manage the diagnostics session:

*   **Refresh Button** (`btnRefresh`): Queries the target application for the latest ViewModel tree. Useful if new windows were opened, user controls were dynamically loaded, or the UI layout changed.
*   **Clear History Button** (`btnClearHistory`): Clears the current command execution history list.

---

## Technical Integration Details

The panel communicates with the target application using standard Compiled Bindings:
*   View: `CdpInspectorApp.Views.MvvmView` (`MvvmView.axaml`)
*   ViewModel: `CdpInspectorApp.ViewModels.MvvmViewModel` (`MvvmViewModel.cs`)
*   Under the hood, when `CdpInspectorApp` connects, it enables the `Mvvm` CDP domain. It registers event handlers for `Mvvm.propertyChanged` and `Mvvm.commandExecuted` to dynamically update the tree and command history grids without requiring full manual page refreshes.
