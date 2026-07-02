---
title: Headless Test Adapter
---

# Headless Test Adapter

The Headless Test Adapter enables automated execution of `.flow.yaml` test files in CI/CD pipelines without requiring a visible display, a running Inspector, or manual interaction. It bridges the Test Studio's YAML test format with headless Avalonia runtime execution.

## Overview

The adapter runs `.flow.yaml` files by:
1. Starting a headless Avalonia application instance
2. Initializing a CDP server on a local port
3. Parsing the YAML flow into `TestStudioStepModel` instances
4. Executing each step through CDP protocol commands
5. Generating reports and capturing screenshots
6. Returning pass/fail exit codes for CI integration

## Setup

### Install the Package

```bash
dotnet add package Chrome.DevTools.Avalonia --prerelease
```

### Configure the Test Host

Create a headless test host that starts your application and runs flows:

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.Diagnostics.Cdp;

public class TestHost
{
    public static async Task<int> RunFlowAsync(string flowPath, int port = 9222)
    {
        var builder = AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());

        using var app = builder.SetupWithoutStarting();

        CdpServer.Start(port);

        try
        {
            var runner = new FlowRunner(port);
            var result = await runner.ExecuteAsync(flowPath);

            return result.AllPassed ? 0 : 1;
        }
        finally
        {
            CdpServer.Stop();
        }
    }
}
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: UI Tests

on: [push, pull_request]

jobs:
  ui-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run UI test flows
        run: |
          dotnet run --project tests/UITests -- \
            --flows tests/flows/ \
            --output test-results/ \
            --report html,pdf \
            --video
```

### Command Line Usage

```bash
# Run a single flow
dotnet run --project tests/UITests -- --flow tests/flows/login.flow.yaml

# Run all flows in a directory
dotnet run --project tests/UITests -- --flows tests/flows/

# With report generation
dotnet run --project tests/UITests -- \
  --flows tests/flows/ \
  --output ./reports/ \
  --report html,pdf

# With video recording
dotnet run --project tests/UITests -- \
  --flows tests/flows/ \
  --output ./reports/ \
  --video
```

## xUnit Integration

Use the Avalonia Headless xUnit adapter to run flows as standard test methods:

```csharp
using Avalonia.Headless.XUnit;
using Xunit;

public class FlowTests
{
    [AvaloniaTheory]
    [InlineData("tests/flows/login.flow.yaml")]
    [InlineData("tests/flows/dashboard.flow.yaml")]
    [InlineData("tests/flows/settings.flow.yaml")]
    public async Task RunFlow(string flowPath)
    {
        var result = await TestHost.RunFlowAsync(flowPath);
        Assert.Equal(0, result);
    }
}
```

## Test Execution Flow

**Test execution flow:**

1. Parse `.flow.yaml`
2. Initialize Headless App
3. Start CDP Server
4. Connect CDP Session
5. **For each step:**
   1. Resolve selector via `DOM.querySelector`
   2. Execute action via CDP
   3. Capture screenshot
   4. **Step passed?**
      - ✅ Yes → Mark Passed → continue to next step
      - ❌ No → Mark Failed + capture error → continue to next step
6. Generate reports
7. Return exit code

## Environment Configuration

### Selecting Environments

Pass environment names to override default variables:

```bash
dotnet run --project tests/UITests -- \
  --flow tests/flows/login.flow.yaml \
  --env staging
```

### Environment Files

Place `.env.yaml` files alongside your flow files:

```yaml
# tests/flows/.env.yaml
environments:
  - name: local
    vars:
      BASE_URL: "http://127.0.0.1:9222"
      USERNAME: "dev-user"
  - name: staging
    vars:
      BASE_URL: "http://staging:9222"
      USERNAME: "test-user"
```

## Parallel Execution

Run multiple flows in parallel with different CDP ports:

```csharp
var flows = Directory.GetFiles("tests/flows/", "*.flow.yaml");
var tasks = flows.Select((flow, index) =>
    TestHost.RunFlowAsync(flow, port: 9222 + index));

var results = await Task.WhenAll(tasks);
var allPassed = results.All(r => r == 0);
```

:::warning
Each parallel flow needs its own CDP port and headless application instance. Do not share ports between concurrent test executions.
:::

## Output Artifacts

The headless adapter generates the same artifacts as interactive Test Studio execution:

| Artifact | Location | Content |
|----------|----------|---------|
| HTML Report | `{output}/report.html` | Step-by-step results with screenshots |
| PDF Report | `{output}/report.pdf` | Print-ready test documentation |
| Step Screenshots | `{output}/images/step_*_screenshot.png` | Per-step visual evidence |
| Video Frames | `{output}/images/frame_*.jpg` | Screencast frames for replay |
| Test Log | `{output}/test.log` | Detailed execution log |

## Next Steps

- [Test Studio](/articles/test-studio) — Visual test editing workspace
- [YAML Test Format](/articles/yaml-test-format) — Flow file specification
- [Test Reports](/articles/test-reports) — Report output details
- [Video Recording](/articles/video-recording) — Screencast capture
- [Build, Test, and Release](/articles/build-test-release) — CI/CD pipeline setup
