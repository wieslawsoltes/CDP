---
title: Application Panel
description: Technical guide to application resource editing, SQLite database tables browsing, custom SQL consoles, and storage auditing in the CDP Inspector for Avalonia.
---

# Application Panel

The **Application Panel** provides inspection and editing tools for persistent stores, cache, application state, and global resources in the running Avalonia application. It serves as a unified manager for storage backends, configuration databases, application assets, and cookies.

---

## 1. Application Navigation Sidebar

The left sidebar of the panel hosts the `treeAppNav` navigation tree, which organizes resources into hierarchical categories:
- **Application Resources**: View and edit global resource dictionaries.
- **Local Storage**: Key-value pairs stored in local files.
- **Cookies**: Simulated cookies for testing network authentication.
- **SQLite Databases**: Active SQLite databases found within the workspace.
- **Background Services**: Monitors background events like Service Workers or telemetry logs.
- **IndexedDB**: Object store browser for structured local databases.

Selecting a node dynamically updates the main pane to display the corresponding editor.

---

## 2. Global Application Resources Editor

The resources panel reads and writes properties from the global `Application.Current.Resources` dictionary in real-time.

### Resources Grid (`lstApplicationResources`)
Displays active resources:
- **Key Name**: The resource key identifier (e.g. `ThemeAccentColor`, `DefaultFontSize`).
- **Value**: The current resource value (e.g. Hex color strings, brush definitions, numeric constants).
- **Delete Resource**: Clicking `btnDeleteResource` removes the resource key from the live dictionary.

### Add / Edit Form
The modifier form allows updating the resource dictionary:
- **Resource Key Name**: The key to target or create.
- **Value Input**: The value to set (supporting Hex colors, solid color brushes, and numbers).
- **Save Resource**: Clicking `btnSaveResource` updates the value of an existing key.
- **Add New**: Clicking `btnAddResource` inserts a new key-value pair.
- **Real-Time Rendering**: Saving updates triggers immediate theme and styling updates across the entire application interface.

---

## 3. SQLite Database Browser

The database viewer allows developers to inspect local SQLite files used by the application for data storage.

### Table List and Record Browser
- **Tables list (`lstTables`)**: Selecting a SQLite database file displays its tables in a list (e.g. `Users`, `Settings`, `Transactions`).
- **Table Data Grid (`dgActiveTable`)**: Selecting a table loads its records. The columns of the grid are generated programmatically to match the schema of the selected table.

### Custom SQL Command Console
The **SQL Console** tab provides an interactive query console for executing SQL queries on the active database:
- **Query Input (`txtCustomSql`)**: A multi-line text editor supporting query input.
- **Execute Query**: Clicking `btnRunQuery` sends the SQL command to the database.
- **Console Result Grid (`dgConsoleResult`)**: Displays the rows returned by the query. Columns are generated dynamically based on the query result set. Both standard `SELECT` read queries and data-modifying queries (`INSERT`, `UPDATE`, `DELETE`) are supported.

---

## 4. Local Storage, Cookie, and IndexedDB Editors

### Local Storage Editor
- **Storage Items Grid (`lstStorageItems`)**: View, add, save, or delete key-value pairs stored in local application settings.

### Cookie Manager
- **Active Cookies Grid (`lstCookies`)**: Renders simulated cookies with attributes: Name, Value, Domain, Path, and Expiry.
- **Form Controls**: Add new cookies or edit existing ones, and delete keys using `btnDeleteCookie`.

### IndexedDB Explorer
- **Object Stores**: Lists object stores found inside IndexedDB database instances.
- **Object Store Records Grid (`dgIndexedDBRecords`)**: Displays stored records in programmatically generated columns.
- **Clear Store**: Clicking `Clear Object Store` wipes all records from the selected object store.

---

## 5. Background Services telemetry Monitor

The **Background Services** tab records telemetry events, such as push notifications, background syncs, or worker updates:
- **Recording Control**: Clicking `ToggleBackgroundRecordingCommand` starts or stops background event logging.
- **Events Log (`dgBackgroundEvents`)**: Displays a log of recorded events:
  - **Timestamp**: Time of event occurrence.
  - **Event**: Action name (e.g. `PeriodicBackgroundSync`, `PushMessage`).
  - **Origin**: Source domain or component origin.
  - **SW Reg ID**: Service Worker Registration ID.
  - **Instance ID**: Unique instance identifier.
- **Event Metadata (`dgMetadata`)**: Selecting an event displays its parameters and payloads in a key-value grid (e.g., payload text, push priority).

---

## 6. Chrome Parity Simulator

For application features that are not natively supported in desktop environments (like Service Workers, background fetch, or web cookies), the CDP server provides a **Chrome Parity Simulator**:
- **Protocol Mock Stubs**: Returns mock values to the inspector client to satisfy browser CDP specifications.
- **Status Dashboard**: Displays the active connection details (e.g. `ws://localhost:9222/devtools/browser`) and simulator state, allowing developers to test web-like inspector workflows on desktop applications.

---

## 7. Underlying CDP Protocol Mappings

The Application Panel maps actions to standard CDP domains:

### Query SQLite Databases
```json
{
  "id": 1,
  "method": "Database.getDatabaseTableNames",
  "params": { "databaseId": "users-db" }
}
```

### Execute SQL Query
```json
{
  "id": 2,
  "method": "Database.executeSQL",
  "params": {
    "databaseId": "users-db",
    "query": "SELECT * FROM Users WHERE IsActive = 1"
  }
}
```

### Cookie Telemetry
```json
{
  "id": 3,
  "method": "Network.getCookies"
}
```

### Background Service Control
```json
{
  "id": 4,
  "method": "BackgroundService.startObserving",
  "params": { "service": "backgroundSync" }
}
```

This diagnostic layout allows developers to inspect application databases, cache stores, and global resources using a familiar Chrome DevTools interface.
