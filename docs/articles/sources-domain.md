---
title: Sources Domain
---

# Sources Domain

The `Sources` domain provides Chrome DevTools Protocol (CDP) clients with workspace file system access within the target Avalonia application process. It enables remote inspection of source files, loading of view templates (XAML/AXAML), editing of configuration or code files, and performing full-text search across the project workspace directly within a CDP session.

This domain is especially valuable for development tools, test-studio recorders, and agentic coding workflows that need to locate, read, or modify application files dynamically at runtime without requiring separate filesystem access or platform-specific OS integrations.

---

## Architecture & Design

The `Sources` domain interacts with a scoped subset of the host machine's filesystem, representing the project workspace. Rather than exposing the entire filesystem of the target machine, it restricts access to a designated root directory and applies selective filtering to keep file listings clean and relevant to the application codebase.

```
+--------------------------------------------------------------+
|                         Host Filesystem                      |
|                                                              |
|   /Users/username/Projects/                                  |
|     ├── NotMyProject/ (Hidden)                               |
|     └── CDP/          <======== [Detected Workspace Root]    |
|           ├── .git/   (Excluded Folder)                      |
|           ├── bin/    (Excluded Folder)                      |
|           └── src/                                           |
|                 ├── MainWindow.axaml     (Allowed Extension) |
|                 ├── MainWindow.axaml.cs  (Allowed Extension) |
|                 └── temp_config.raw      (Filtered Out)      |
+--------------------------------------------------------------+
```

### Workspace Root Detection

To locate the boundary of the project workspace, the CDP server runs a traversal search at startup:

1. It retrieves the current working directory of the application process using `Directory.GetCurrentDirectory()`.
2. It iteratively climbs up parent directories looking for any of the following boundary indicators:
   - A `.git` directory.
   - Any solution file matching `*.sln`.
   - Any solution file matching `*.slnx`.
3. If one of these indicators is found, the containing directory is designated as the **Workspace Root**.
4. If the traversal reaches the root of the filesystem without finding a boundary indicator, it falls back to using the current working directory.

### Directory Exclusions

To avoid cluttering the interface with compiler outputs, source control metadata, or external package directories, the `Sources` domain completely ignores the following directories (case-insensitive) during recursive file operations and searches:

- `bin` (Compiler outputs)
- `obj` (Intermediate build files)
- `.git` (Git repository metadata)
- `.vs` (Visual Studio user settings)
- `.idea` (JetBrains project configurations)
- `node_modules` (NPM packages)

### File Extension Filtering

The `Sources` domain applies a strict whitelist to files exposed through listings or searches. Only files with the following extensions (case-insensitive) are processed:

| File Extension | Target Type |
|---|---|
| `.cs` | C# Source Files |
| `.axaml` | Avalonia XML Markup Files |
| `.xaml` | WPF/Generic XML Markup Files |
| `.json` | JSON Configurations, step recordings, and maps |
| `.md` | Markdown documentation files |
| `.xml` | XML Configuration and documentation files |
| `.csproj` | MSBuild C# Project Files |

---

## Security Model & Path Boundaries

To prevent directory traversal attacks and unauthorized access to arbitrary files on the host machine, the `Sources` domain enforces strict path boundary validation on any method receiving a file path parameter (`getFileContent` and `setFileContent`).

### The Traversal Check Algorithm

Every relative path supplied by the CDP client is normalized and validated against the detected Workspace Root:

1. **Resolution:** The path is combined with the workspace root and resolved to its canonical absolute path:
   ```csharp
   string fullPath = Path.GetFullPath(Path.Combine(root, relPath));
   ```
2. **Relative Derivation:** The absolute path is converted back into a relative path from the perspective of the workspace root:
   ```csharp
   string relative = Path.GetRelativePath(root, fullPath);
   ```
3. **Boundary Check:** The derived relative path is analyzed:
   - If it begins with `..` (indicating a parent directory escape), it is outside the workspace root.
   - If it is rooted (indicating an absolute path to a different location), it is outside the workspace root.
   - If either condition is true, an access violation exception is thrown and the request fails.

```csharp
if (relative.StartsWith("..") || Path.IsPathRooted(relative))
{
    throw new Exception("Access denied: path is outside workspace root.");
}
```

This ensures that clients cannot use paths like `../../../../etc/passwd` or `/etc/passwd` to read or write files outside the workspace root boundaries.

---

## Protocol Methods Reference

This section details the primary methods implemented by the `Sources` domain in the `Avalonia.Diagnostics.Cdp` package.

### 1. Sources.getWorkspaceFiles

Retrieves a recursive listing of all files located within the project workspace that match the extension whitelist and are not in excluded folders.

* **Parameters:** None.
* **Returns:**
  - `files` (array of objects): A list of files present in the workspace.
    - `path` (string): The workspace-relative path of the file (using forward slashes `/`).
    - `name` (string): The file name with extension.
    - `size` (integer): The size of the file in bytes.

#### Request Example
```json
{
  "id": 200,
  "method": "Sources.getWorkspaceFiles"
}
```

#### Response Example
```json
{
  "id": 200,
  "result": {
    "files": [
      {
        "path": "src/CDP.Inspector.Shared/ViewModels/SourcesViewModel.cs",
        "name": "SourcesViewModel.cs",
        "size": 25898
      },
      {
        "path": "src/Chrome.DevTools.Protocol/Domains/SourcesDomain.cs",
        "name": "SourcesDomain.cs",
        "size": 7638
      },
      {
        "path": "samples/CdpSampleApp/MainWindow.axaml",
        "name": "MainWindow.axaml",
        "size": 1823
      }
    ]
  }
}
```

---

### 2. Sources.getFileContent

Retrieves the text content of a specific file from the workspace.

* **Parameters:**
  - `path` (string): The workspace-relative path of the file.
* **Returns:**
  - `content` (string): The complete text content of the file.
* **Errors:**
  - Throws `Exception` if the path escapes the workspace root boundary.
  - Throws `Exception` if the file does not exist.

#### Request Example
```json
{
  "id": 201,
  "method": "Sources.getFileContent",
  "params": {
    "path": "samples/CdpSampleApp/MainWindow.axaml"
  }
}
```

#### Response Example
```json
{
  "id": 201,
  "result": {
    "content": "<Window xmlns=\"https://github.com/avaloniaui\"\n        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n        Title=\"CDP Sample Target App\">\n    <Grid>\n        <Button Name=\"btnClickMe\" Content=\"Click Me!\" />\n    </Grid>\n</Window>"
  }
}
```

#### Request Violation Error Response
```json
{
  "id": 201,
  "error": {
    "code": -32000,
    "message": "Access denied: path is outside workspace root."
  }
}
```

---

### 3. Sources.setFileContent

Overwrites the content of an existing file or creates a new file at the specified workspace-relative path.

* **Parameters:**
  - `path` (string): The workspace-relative path of the file to write.
  - `content` (string): The text content to write into the file.
* **Returns:**
  - `success` (boolean): `true` if the file was written successfully.
* **Errors:**
  - Throws `Exception` if the path escapes the workspace root boundary.

#### Request Example
```json
{
  "id": 202,
  "method": "Sources.setFileContent",
  "params": {
    "path": "samples/CdpSampleApp/MainWindow.axaml",
    "content": "<Window xmlns=\"https://github.com/avaloniaui\"\n        Title=\"Updated Window Title\">\n    <Button Name=\"btnClickMe\" Content=\"Click Me!\" />\n</Window>"
  }
}
```

#### Response Example
```json
{
  "id": 202,
  "result": {
    "success": true
  }
}
```

---

### 4. Sources.searchInWorkspace

Searches recursively within the workspace files for a specific query string. It reads whitelisted files line-by-line and returns matches including line numbers and contents.

* **Parameters:**
  - `query` (string): The substring query to search for.
  - `caseSensitive` (boolean, optional): Whether to perform a case-sensitive search. Defaults to `false`.
* **Returns:**
  - `matches` (array of objects): A list of occurrences containing:
    - `path` (string): The workspace-relative path of the file.
    - `lineNumber` (integer): The 1-indexed line number where the match was found.
    - `lineContent` (string): The raw content of the matching line.

#### Request Example
```json
{
  "id": 203,
  "method": "Sources.searchInWorkspace",
  "params": {
    "query": "GetWorkspaceRoot",
    "caseSensitive": true
  }
}
```

#### Response Example
```json
{
  "id": 203,
  "result": {
    "matches": [
      {
        "path": "src/Chrome.DevTools.Protocol/Domains/SourcesDomain.cs",
        "lineNumber": 17,
        "lineContent": "                    string root = GetWorkspaceRoot();"
      },
      {
        "path": "src/Chrome.DevTools.Protocol/Domains/SourcesDomain.cs",
        "lineNumber": 70,
        "lineContent": "    private static string GetWorkspaceRoot()"
      }
    ]
  }
}
```

---

## Client Integration Reference

In the `CdpInspectorApp` client, the workspace view is driven by the `SourcesViewModel` class.

### Initialization Flow

During startup or target connection, the inspector performs the following actions:

1. Enables debugger support using `Debugger.enable` so source navigation and breakpoints function properly.
2. Involves the `Sources.getWorkspaceFiles` command to build a structured visual file explorer:
   - Receives the flat list of relative paths.
   - Splits each path by `/` boundaries to build hierarchical `WorkspaceFileNode` tree nodes.
   - Displays folders and files with corresponding icons (e.g., standard Fluent system icons for folders and files).

### File Operations

When a user selects a file node in the explorer:
- If the node is a directory, the inspector expands the branch.
- If the node is a file, the inspector calls `Sources.getFileContent` with the node's relative path and updates the editor preview pane with the text.
- Saving edits in the code preview executes `Sources.setFileContent` asynchronously to synchronize modifications directly to the host workspace filesystem.
