---
title: CLI Automation Tool (cdp-cli)
---

# CLI Automation Tool (cdp-cli)

The **CDP Inspector CLI (`cdp-cli`)** is a command-line utility packaged as a `.NET Global Tool` under `Chrome.DevTools.Cli`. It enables developers, QA engineers, and automated CI/CD agents to scan target processes, run visual YAML tests and suites, extract tree hierarchies, execute real-time evaluations, stream logging events, and run individual user actions programmatically.

## Installation

Ensure you have the .NET SDK (10.0+) installed on your path, then run:

```bash
# Install the tool globally
dotnet tool install -g Chrome.DevTools.Cli

# To verify installation, check the help details
cdp-cli --help
```

---

## Global Host & Target Matching Options

Most commands communicate with a target application running an embedded CDP server. You can specify which target to connect to using these common options:

* `-h, --host <url>`: The HTTP/WebSocket address of the CDP server host. Defaults to `http://127.0.0.1:9222`.
* `-t, --target <id>`: Specifies the exact target window/page UUID (e.g. `a634ddf6-1b79-4128-a744-929ef5d63003`).
* `-n, --target-name <name>`: Matches a target window by a case-insensitive substring of its title (e.g. `--target-name sample`).

If neither target ID nor target name is specified, the CLI will query the host:
* If only one target is found, it will automatically connect to it.
* If multiple targets are found, it will warn the user and default to the first target.

---

## Command Reference

### 1. list-targets
Queries the host and displays a list of all active, debuggable target windows/pages, showing target titles, unique IDs, and WebSocket endpoints.

```bash
cdp-cli list-targets --host http://127.0.0.1:9222
```

**Example Output**:
```text
Found 1 targets on http://127.0.0.1:9222:
1. Avalonia CDP Inspector Sample (ID: a634ddf6-1b79-4128-a744-929ef5d63003)
   WS: ws://127.0.0.1:9222/devtools/page/a634ddf6-1b79-4128-a744-929ef5d63003
```

---

### 2. run
Executes automated UI tests defined in the Test Studio YAML format. It can run a single `.yaml` test flow or sequentially run an entire folder structure containing multiple `.yaml` flow files.

```bash
# Run a single test flow with video frame capturing and PDF/HTML report generation
cdp-cli run scratch/test_flow.yaml --report --video --output-dir TestReports

# Run all YAML test flows in a directory sequentially
cdp-cli run integration-tests/ --report --output-dir TestReports
```

#### Orchestrated Auto-Launching
For complete headless isolation, the runner can auto-launch the application under test before starting execution and clean it up afterwards:
```bash
cdp-cli run scratch/test_flow.yaml --auto-launch "dotnet run --project samples/CdpSampleApp" --timeout 45000
```

#### Options:
* `-o, --output-dir <path>`: Output directory for test reports (default: `TestReports`).
* `-v, --video`: Capture screencast video frames (JPEG) during test execution. Saved under the run folder inside `images/`.
* `-r, --report`: Generate matching HTML (`index.html`) and PDF (`report.pdf`) reports detailing step status, durations, logs, and screenshots.
* `-e, --env <KEY=VALUE>`: Defines environment variables for YAML script template interpolation. Can be defined multiple times.
* `--auto-launch <path>`: Application executable path to start before testing.
* `--auto-launch-args <args>`: Arguments passed to the auto-launched app.
* `--timeout <ms>`: Step or execution timeout threshold in milliseconds (default: `30000`).

---

### 3. hierarchy
Retrieves and prints the application tree hierarchy. This is equivalent to `maestro hierarchy` and is highly useful for scanning controls, layout bounds, names, and automation IDs to construct selectors.

```bash
# Print the Accessibility (AX) Tree in indented text layout (Default)
cdp-cli hierarchy --type accessibility --format text

# Dump the DOM Visual Tree in JSON format
cdp-cli hierarchy --type visual --format json
```

**Text Format Example**:
```text
window name="Avalonia CDP Inspector Sample" value=""
  WindowChrome name="WindowChrome" value=""
  Panel name="" value=""
    Border name="" value=""
      Grid name="" value=""
        StaticText name="Avalonia CDP Inspector Sample" value=""
        tab name="" value=""
          Border name="" value=""
            DockPanel name="" value=""
              ItemsPresenter name="" value=""
                WrapPanel name="" value=""
                  tab name="Home" value=""
...
```

---

### 4. eval
Evaluates a C# expression on the target application process. Evaluated scripts have access to global variables like `Window`, `Control`, `DataContext`, `ViewModel`, and browser-like selector shortcuts.

```bash
# Query the window title
cdp-cli eval "Window.Title"

# Query the status label text using document selector query
cdp-cli eval "document.querySelector('#lblStatus').text"

# Read viewmodel count
cdp-cli eval "((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count"
```

---

### 5. action
Executes a single, interactive user interface action against a selector in the target application. This provides immediate, ad-hoc control over application state directly from bash scripts.

```bash
# Tap/click a button
cdp-cli action tap "#btnClickMe"

# Insert text into an input field
cdp-cli action input "#txtSearch" "input text query"

# Clear text in an input field
cdp-cli action clear "#txtSearch"

# Assert that an element is visible (fails with exit code 1 if not visible)
cdp-cli action assert-visible "#lblWelcome"

# Scroll a scroll-viewer control
cdp-cli action scroll "#scrollViewer" down
```

---

### 6. logs
Streams console messages, outbound network requests, and protocol events live from the target application to stdout in real-time. This command runs indefinitely until stopped with `Ctrl+C`.

```bash
# Stream all logging domains
cdp-cli logs --type all

# Stream only runtime console messages
cdp-cli logs --type console
```

---

## Exit Codes & Automation Integration

The `cdp-cli` tool follows standard POSIX exit-code conventions, making it easy to integrate into exit-asserting bash scripts and CI/CD pipelines (e.g. GitHub Actions, GitLab CI, Azure Pipelines):

* **`0`**: Success. The test flow/suite passed completely, or the queried action succeeded.
* **`1`**: Failure. A test step failed, an assertion failed, a target connection timed out, or an invalid parameter configuration was provided.
