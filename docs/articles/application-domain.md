---
title: Application Domain
---

# Application Domain in CDP Avalonia

The Chrome DevTools Protocol (CDP) **Application Domain** in `Avalonia.Diagnostics.Cdp` provides a bridge for inspecting and manipulating the application-level state. It enables two main categories of operations:

1. **Global Resource Management**: Querying, mutating, and deleting application-level resources (such as colors, brushes, strings, numeric values, and styles) registered in `Application.Current.Resources`.
2. **Local SQLite Database Inspection**: Auto-discovering local SQLite database files (`.sqlite` or `.db`) within the project hierarchy, listing their table structures, and running arbitrary SQL queries directly against them.

This domain is designed to assist automation agents, visual regression tools, and developers in dynamically modifying application themes, checking global state flags, and verifying persistent SQLite database states during testing.

---

## Core Capabilities

### Global Resource Management
Avalonia applications use a dictionary-based resource system (`ResourceDictionary`) to define themes, shared brushes, margins, and styles. The Application Domain allows remote clients to:
*   List all global keys, their fully qualified type names, and string representations.
*   Update resource values at runtime (with smart type coercion for colors, brushes, integers, doubles, and booleans).
*   Remove resources to test fallback scenarios or trigger dynamic UI updates.

### Local Database Inspection
For applications that persist local configurations, user preferences, or offline caches in SQLite, the Application Domain acts as a lightweight database client. It automatically scans the workspace directory (excluding binary, build, and version control directories) to expose database files, their tables, and query results to the CDP client.

---

## Command Summary

The following table summarizes the JSON-RPC methods exposed by the Application Domain:

| Method Name | Category | UI Thread Dispatched? | Description |
| :--- | :--- | :---: | :--- |
| `Application.getResources` | Resource | **Yes** | Returns all keys and values currently registered in the global application resource dictionary. |
| `Application.setResource` | Resource | **Yes** | Adds or updates a resource value, automatically parsing colors, brushes, numbers, and booleans. |
| `Application.deleteResource` | Resource | **Yes** | Deletes a specified resource from the global resource dictionary. |
| `Application.getDatabases` | Database | No | Scans the workspace directories to find local SQLite files (`.db` or `.sqlite`). |
| `Application.getDatabaseTableNames` | Database | No | Lists all user-defined table names within a specified SQLite database file. |
| `Application.executeSQL` | Database | No | Runs an arbitrary SQL query (SELECT, PRAGMA, INSERT, UPDATE, etc.) and returns columns and row arrays. |

---

## Detailed Command Reference

### 1. `Application.getResources`

Retrieves all resource keys, their runtime C# type names, and string representations from `Application.Current.Resources`.

#### Parameters
*None.*

#### Response Payload
*   `resources`: An array of resource descriptors containing:
    *   `key`: The string representation of the resource key.
    *   `type`: The fully qualified name of the resource's C# type.
    *   `value`: The string representation of the resource value.

#### JSON-RPC Example

**Request:**
```json
{
  "id": 201,
  "method": "Application.getResources",
  "params": {}
}
```

**Response:**
```json
{
  "id": 201,
  "result": {
    "resources": [
      {
        "key": "SystemAccentColor",
        "type": "Avalonia.Media.Color",
        "value": "#FF007ACC"
      },
      {
        "key": "HighlightBrush",
        "type": "Avalonia.Media.SolidColorBrush",
        "value": "SolidColorBrush #FF007ACC"
      },
      {
        "key": "IsDarkThemeEnabled",
        "type": "System.Boolean",
        "value": "True"
      },
      {
        "key": "DefaultFontSize",
        "type": "System.Double",
        "value": "14"
      }
    ]
  }
}
```

---

### 2. `Application.setResource`

Registers a new resource or overwrites an existing resource in `Application.Current.Resources`. This command employs smart type parsing to convert the input string into concrete Avalonia and .NET types.

#### Parameters
*   `key` (string, required): The unique key identifier of the resource.
*   `value` (string, required): The string-encoded value to set.

#### Type Coercion Rules
If an existing resource with the same key is found, the domain uses its type to parse the incoming value string:
*   **`double`**: Parsed via `double.TryParse`.
*   **`int`**: Parsed via `int.TryParse`.
*   **`bool`**: Parsed via `bool.TryParse`.
*   **`Avalonia.Media.Color`**: Parsed via `Color.TryParse`.
*   **`Avalonia.Media.SolidColorBrush`**: Parsed by converting the value into an Avalonia `Color` first, and wrapping it in a new `SolidColorBrush`.

If no existing resource matches the key, the domain falls back to the following heuristics:
*   If the value starts with `#` and is exactly 7 or 9 characters long (e.g., `#FFCA28` or `#FFFFCA28`), it is parsed as a color and stored as a `SolidColorBrush`.
*   If the value can be parsed as a double, it is stored as a `double`.
*   If the value can be parsed as a boolean, it is stored as a `bool`.
*   Otherwise, the value is stored as a raw `string`.

#### JSON-RPC Example

**Request (Setting a Brush Color):**
```json
{
  "id": 202,
  "method": "Application.setResource",
  "params": {
    "key": "HighlightBrush",
    "value": "#FF4CAF50"
  }
}
```

**Response:**
```json
{
  "id": 202,
  "result": {}
}
```

---

### 3. `Application.deleteResource`

Removes a resource key and its associated value from the application's global resource dictionary.

#### Parameters
*   `key` (string, required): The key identifier of the resource to delete.

#### JSON-RPC Example

**Request:**
```json
{
  "id": 203,
  "method": "Application.deleteResource",
  "params": {
    "key": "TemporaryHighlightColor"
  }
}
```

**Response:**
```json
{
  "id": 203,
  "result": {}
}
```

---

### 4. `Application.getDatabases`

Recursively scans the application's current working directory to locate files ending with `.db` or `.sqlite`. It filters out build directories (`bin`, `obj`, `publish`) and version control/agent config directories (`.git`, `.gemini`) to present clean workspace results.

#### Parameters
*None.*

#### Response Payload
*   `databases`: An array of absolute file paths pointing to the discovered SQLite databases.

#### JSON-RPC Example

**Request:**
```json
{
  "id": 204,
  "method": "Application.getDatabases",
  "params": {}
}
```

**Response:**
```json
{
  "id": 204,
  "result": {
    "databases": [
      "/Users/wieslawsoltes/GitHub/CDP/samples/CdpSampleApp/local_cache.db",
      "/Users/wieslawsoltes/GitHub/CDP/samples/CdpInspectorApp/inspector_settings.sqlite"
    ]
  }
}
```

---

### 5. `Application.getDatabaseTableNames`

Retrieves a list of all user-defined table names within a target SQLite database file.

#### Parameters
*   `databasePath` (string, required): The absolute path to the SQLite database file.

#### JSON-RPC Example

**Request:**
```json
{
  "id": 205,
  "method": "Application.getDatabaseTableNames",
  "params": {
    "databasePath": "/Users/wieslawsoltes/GitHub/CDP/samples/CdpSampleApp/local_cache.db"
  }
}
```

**Response:**
```json
{
  "id": 205,
  "result": {
    "tables": [
      "UserProfile",
      "RecentConnections",
      "SavedScenarios"
    ]
  }
}
```

---

### 6. `Application.executeSQL`

Executes an arbitrary SQL statement against a targeted database file. 

*   For query operations (statements starting with `SELECT` or `PRAGMA`), it returns the columns and all resulting rows.
*   For non-query operations (such as `UPDATE`, `INSERT`, or `DELETE`), it executes the SQL and returns a virtual single-column table representing the number of rows affected.

#### Parameters
*   `databasePath` (string, required): The absolute path to the SQLite database file.
*   `query` (string, required): The SQL statement to run.

#### JSON-RPC Example (SELECT Query)

**Request:**
```json
{
  "id": 206,
  "method": "Application.executeSQL",
  "params": {
    "databasePath": "/Users/wieslawsoltes/GitHub/CDP/samples/CdpSampleApp/local_cache.db",
    "query": "SELECT Id, Name, IsActive FROM UserProfile LIMIT 2;"
  }
}
```

**Response:**
```json
{
  "id": 206,
  "result": {
    "columns": [
      "Id",
      "Name",
      "IsActive"
    ],
    "rows": [
      [1, "Developer Admin", true],
      [2, "Guest Tester", false]
    ]
  }
}
```

#### JSON-RPC Example (UPDATE Query)

**Request:**
```json
{
  "id": 207,
  "method": "Application.executeSQL",
  "params": {
    "databasePath": "/Users/wieslawsoltes/GitHub/CDP/samples/CdpSampleApp/local_cache.db",
    "query": "UPDATE UserProfile SET IsActive = true WHERE Id = 2;"
  }
}
```

**Response:**
```json
{
  "id": 207,
  "result": {
    "columns": [
      "Rows Affected"
    ],
    "rows": [
      [1]
    ]
  }
}
```

---

## Thread Safety and Dispatching

Because Avalonia's visual tree and resource styling systems are thread-confined, any direct mutation or reading of resources outside the main UI thread will result in invalid operation exceptions or undefined visual glitches.

To guarantee safety, `ApplicationDomain` dispatches resource operations (`getResources`, `setResource`, `deleteResource`) onto the main UI loop using:

```csharp
await Dispatcher.UIThread.InvokeAsync(() => {
    // Read, write, or delete resources safely here
});
```

Conversely, file-system scans and database operations (`getDatabases`, `getDatabaseTableNames`, `executeSQL`) do not touch visual elements or Avalonia objects. They run synchronously on background worker threads, preventing blocks on the main rendering loop when handling disk operations or running intensive SQL queries.

---

## Practical Scenarios & Automations

### Dynamic Styling Mutation
During UI/UX testing or visual regression suites, agents can verify how the application behaves in high-contrast situations or theme changes without restarting the app process:

1. Retrieve the list of resources to identify theme variables.
2. Use `Application.setResource` with a target theme color:
   ```json
   {
     "key": "SystemAccentColor",
     "value": "#ffff5252"
   }
   ```
3. Take a screenshot using the `Page` domain to confirm that the theme colors have repainted correctly across controls.

### Verifying Persistence State
When an automated flow finishes (e.g., saving user configurations inside a preview target app), the test runner can check that database tables have updated:

1. Query databases to obtain the path using `Application.getDatabases`.
2. Retrieve the row count using `Application.executeSQL`:
   ```sql
   SELECT COUNT(*) FROM SavedScenarios WHERE Status = 'Completed';
   ```
3. Assert that the counts match expected verification thresholds.

---

## Source Reference

For details on the C# handler implementation, see:
*   `ApplicationDomain.cs`
