# MVVM DevTools Domain

To enable deep diagnostics and testing of Model-View-ViewModel (MVVM) patterns within desktop applications, the Avalonia CDP server features a custom protocol domain: **`Mvvm`**.

This domain allows developers and automated agents to traverse the active ViewModel tree, read and modify properties in real-time, subscribe to property change notifications, and inspect the execution history of user interface commands (such as `ReactiveCommand`).

---

## Activating the Domain

Like other CDP domains, the `Mvvm` domain must be enabled before sending queries or receiving events:

```json
// Request
{ "id": 1, "method": "Mvvm.enable" }

// Response
{ "id": 1, "result": {} }
```

When enabled, the server initializes a `SessionMvvmState` instance, which starts tracking ViewModels and their command states. Disabling the domain cleans up all reflection hooks and active subscriptions:

```json
{ "id": 2, "method": "Mvvm.disable" }
```

---

## Retrieving the ViewModel Tree (`Mvvm.getViewModelTree`)

The method `Mvvm.getViewModelTree` crawls the Avalonia visual tree starting from the active window, looking for controls that host unique `DataContext` instances.

### Visual Tree Traversal

The tree walker (`WalkVisualTree`) traverses the visual hierarchy. If it encounters a control whose `DataContext` is non-null and distinct from its parent's `DataContext`, it registers it as a ViewModel node:

*   **Node Identification**: Assigns a stable, unique identifier (e.g., `vm-uuid`) to each ViewModel using a `ConditionalWeakTable`. This prevents retaining objects in memory and avoids garbage collection interference.
*   **Node Metadata**:
    *   `id`: The unique ViewModel identifier.
    *   `type`: The fully qualified type name of the ViewModel.
    *   `controlType`: The fully qualified type name of the host visual control.
    *   `controlName`: The programmatic name of the control (if set via `Name="..."`).
    *   `properties`: An array of public properties exposed by the ViewModel.
    *   `children`: Hierarchical child nodes hosting sub-ViewModels.

### Example Response Payload
```json
{
  "id": 3,
  "result": {
    "tree": [
      {
        "id": "vm-a83d47-f389",
        "type": "CdpInspectorApp.ViewModels.MainWindowViewModel",
        "controlType": "CdpInspectorApp.Views.MainWindow",
        "controlName": "mainWindow",
        "properties": [
          {
            "name": "IsConnected",
            "type": "System.Boolean",
            "value": true,
            "isWritable": false
          },
          {
            "name": "ConnectionStatusText",
            "type": "System.String",
            "value": "Connected to http://127.0.0.1:9222",
            "isWritable": true
          }
        ],
        "children": []
      }
    ]
  }
}
```

---

## Property Editing (`Mvvm.setPropertyValue`)

Agents and UI tools can dynamically update ViewModel properties during runtime. This is extremely useful for setting state directly (e.g., bypassing login flows or feeding mock data).

*   **Method**: `Mvvm.setPropertyValue`
*   **Parameters**:
    *   `viewModelId` (`string`): The identifier of the target ViewModel.
    *   `propertyName` (`string`): The property to modify.
    *   `value` (`any`): The new value.

### Type Conversion Pipeline
The server parses the incoming JSON value and converts it to the property's concrete .NET type on the UI thread:
*   Matches primitives: `string`, `bool`, integers (`int`, `long`), decimals (`double`, `float`, `decimal`).
*   Parses `Guid`, `DateTime`, and `DateTimeOffset`.
*   Supports parsing Enums by string value.
*   Deserializes complex JSON shapes into targeted types using `System.Text.Json`.

---

## Property Change Notifications (`Mvvm.propertyChanged` Event)

If a ViewModel implements the standard .NET `INotifyPropertyChanged` interface, the domain automatically hooks into its changes. 

When a property is changed in the application:
1.  The `PropertyChanged` event is fired by the ViewModel.
2.  The server serializes the updated property value using `SerializeValue`.
3.  The server broadcasts the `Mvvm.propertyChanged` event:
    ```json
    {
      "method": "Mvvm.propertyChanged",
      "params": {
        "viewModelId": "vm-a83d47-f389",
        "propertyName": "ConnectionStatusText",
        "value": "Connecting..."
      }
    }
    ```

---

## Command Execution Tracking (`Mvvm.commandExecuted` Event)

Many ViewModels expose commands implementing `IObservable<T>` (such as ReactiveUI's `ReactiveCommand`). The `Mvvm` domain dynamically subscribes to these observables using a helper reflection utility (`ObservableSubscriptionHelper.SubscribeDynamic`).

Whenever a command executes in the target application:
1.  The observable emits an execution result.
2.  The server captures the emission on the UI thread.
3.  An `Mvvm.commandExecuted` event is pushed to the client:
    ```json
    {
      "method": "Mvvm.commandExecuted",
      "params": {
        "viewModelId": "vm-a83d47-f389",
        "viewModelType": "MainWindowViewModel",
        "commandName": "ConnectCommand",
        "result": "[Success]",
        "timestamp": "2026-07-07T12:00:00.000Z"
      }
    }
    ```

This enables automated tests to assert that commands executed successfully and inspected operations resolved with correct return values.
