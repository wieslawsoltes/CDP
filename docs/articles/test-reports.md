---
title: Test Reports
---

# Test Reports

The CDP Inspector generates comprehensive HTML and PDF test reports after Test Studio execution. Reports include step-by-step results, screenshots, timing data, and pass/fail summaries for audit trails and CI/CD integration.

## Enabling Reports

Enable report generation before running a test flow:

1. Check **Generate Reports** (`#chkTestStudioGenerateReports`) in the Test Studio toolbar
2. Set the **Output Directory** (`#txtTestStudioOutputDirectory`) to your desired path
3. Run the test flow

Reports are generated automatically when execution completes.

## HTML Report

The HTML report is a self-contained, single-file document rendered using SkiaSharp canvas drawing and exported as an HTML page with embedded images.

### Report Structure

| Section | Content |
|---------|---------|
| Header | Test flow name, execution date/time, total duration |
| Summary | Total steps, passed count, failed count, pass rate percentage |
| Step Table | Per-step results with action, selector, status, duration, error message |
| Screenshots | Before/after screenshots for each step embedded as base64 PNG |
| Network Waterfall | HTTP request timeline (when network monitoring is enabled) |
| Footer | Report generation timestamp and CDP version |

### Viewing HTML Reports

After execution, click **View HTML Report** (`#btnViewHtmlReport`) to open the report in your default browser. The report path is stored in `LastReportPath` on the Test Studio view model.

## PDF Report

The PDF report contains the same content as the HTML report in a print-ready format. It is generated using SkiaSharp PDF document rendering.

### Viewing PDF Reports

Click **View PDF Report** (`#btnViewPdfReport`) to open the PDF. The path is stored in `LastPdfReportPath`.

## Report Assets

Reports are accompanied by screenshot assets in the output directory:

```
{outputDirectory}/
├── report.html
├── report.pdf
├── images/
│   ├── step_001_screenshot.png
│   ├── step_002_screenshot.png
│   ├── step_003_screenshot.png
│   └── ...
└── test.log
```

### Step Screenshots

Each step captures a screenshot of the target application at execution time using `Page.captureScreenshot`. Screenshots are:
- Full-window captures at the application's native DPI
- Saved as PNG files in the `images/` subdirectory
- Embedded in the HTML report as base64-encoded inline images

### Network Telemetry

When network monitoring is active, the `NetworkTelemetryProvider` tracks request timing data that is rendered as a waterfall chart in the report. This shows:
- Request start times relative to test start
- Time to first byte (TTFB)
- Download duration
- Request URL and HTTP status

## Report Customization

### Output Directory

Set the output directory programmatically:

```csharp
testStudio.OutputDirectory = "/path/to/reports";
```

Or via CDP:

```json
{
  "id": 1,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.OutputDirectory = \"/tmp/reports\""
  }
}
```

### Report Content

The report renderer uses the `TestReportBuilder` which collects:
- Step execution results from `TestStudioStepModel` instances
- Screenshots from the per-step capture buffer
- Network telemetry from `NetworkTelemetryProvider`
- Timing data from step execution timestamps

## CI/CD Integration

### Archiving Reports

In CI/CD pipelines, archive report artifacts for post-build review:

```yaml
# GitHub Actions
- name: Run UI tests
  run: dotnet run --project tests/UITests -- --flows tests/flows/ --output reports/

- name: Upload test reports
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: test-reports
    path: reports/
```

### Fail on Test Failures

The test runner returns a non-zero exit code when any step fails, integrating naturally with CI systems:

```bash
dotnet run --project tests/UITests -- --flows tests/flows/ --output reports/
# Exit code 0 = all passed, 1 = failures detected
```

## Programmatic Report Access

Access report paths after execution through the view model:

```csharp
// After execution completes
string? htmlPath = testStudio.LastReportPath;
string? pdfPath = testStudio.LastPdfReportPath;

// Check if reports were generated
bool hasReport = !string.IsNullOrEmpty(htmlPath) && File.Exists(htmlPath);
```

Or via CDP Runtime evaluation:

```json
{
  "id": 1,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.LastReportPath"
  }
}
```

## Next Steps

- [Video Recording](/articles/video-recording) — Screencast capture during test execution
- [Test Studio](/articles/test-studio) — Visual test editing workspace
- [Headless Test Adapter](/articles/headless-test-adapter) — CI/CD test execution
