---
title: Video Recording
---

# Video Recording

The CDP Inspector can record test execution sessions as video by capturing screencast frames during Test Studio replay. This provides visual evidence of test runs and enables debugging of timing-dependent failures.

## Enabling Video Recording

Enable video recording before running a test flow:

1. Check **Record Video** (`#chkTestStudioRecordVideo`) in the Test Studio toolbar
2. Set the **Output Directory** (`#txtTestStudioOutputDirectory`) if not already configured
3. Run the test flow — frames are captured automatically

## How It Works

Video recording uses the `Page.startScreencast` CDP method to capture frames from the target application during test execution:

**Screencast capture sequence:**

1. **TestStudio** → **CDP Server**: `Page.startScreencast`
2. *(Loop during execution:)*
   1. **CDP Server** → **Target App**: Render frame
   2. **Target App** → **CDP Server**: Bitmap data
   3. **CDP Server** → **TestStudio**: `Page.screencastFrame` event
   4. **TestStudio** → **TestStudio**: Save frame as JPEG
   5. **TestStudio** → **CDP Server**: `Page.screencastFrameAck`
3. **TestStudio** → **CDP Server**: `Page.stopScreencast`
4. **TestStudio** → **TestStudio**: Compile frames into video

### Frame Capture

During test execution:
1. The CDP server captures frames at a configurable interval
2. Each frame is sent to the Inspector as a `Page.screencastFrame` event containing base64-encoded image data
3. The Inspector saves each frame as a JPEG file in the output directory
4. Frame acknowledgments (`Page.screencastFrameAck`) control backpressure

### Delta Detection

The screencast producer includes a change detection mechanism:
- Each frame is compared to the previous frame
- Only frames with visual changes are transmitted
- A 500ms watchdog timer ensures frames are sent even during idle periods

### Tiled Transfer

For high-resolution displays, the `TiledScreencastProducer` splits large frames into smaller tiles for efficient transmission. This reduces memory pressure and improves streaming performance over WebSocket connections.

## Output Format

Video frames are saved as individual JPEG files in the output directory:

```
{outputDirectory}/
├── images/
│   ├── frame_0001.jpg
│   ├── frame_0002.jpg
│   ├── frame_0003.jpg
│   ├── ...
│   └── frame_0150.jpg
├── report.html
└── report.pdf
```

### Frame Naming

Frames are numbered sequentially starting from `frame_0001.jpg`. The frame count depends on test duration and the capture interval.

## Video Playback Window

After test execution completes, click **Replay Last Video** (`#btnReplayLastVideo`) in the Test Studio toolbar to open the Video Playback Window — a full-featured viewer for reviewing test recordings.

### Layout

The playback window is organized into three sections:

1. **Summary Dashboard** (top bar) — Total steps, passed/failed counts, success rate percentage, total duration
2. **Split Workspace** (center)
   - **Left panel** — Scrollable step list with status, duration, and thumbnails
   - **Right panel** — Video frame viewer with embedded telemetry tabs
3. **Playback Controls** (bottom bar) — Play/pause, seek slider, speed controls

### Step Panel

Each step in the left panel displays:

| Property | Description |
|----------|-------------|
| Index | Step number in the sequence |
| Action | Step action type (e.g., tapOn, assertVisible) |
| Status | Color-coded: green (PASSED), red (FAILED) |
| Selector | Target element selector |
| Value | Input or assertion value |
| Duration | Step execution time |
| Thumbnail | Screenshot captured at step completion |

**Click any step** to seek the video to that step's start time.

### Playback Engine

- **Frame-based playback** using `DispatcherTimer` at ~60fps tick rate
- **Variable speed** — 1x, 2x, 4x playback rates
- **Binary search** for efficient frame-at-time lookup
- **LRU bitmap cache** (max 50 frames) with proper disposal
- **Seek slider** with bidirectional sync (slider↔playback position)
- **Auto-highlight** — The step panel auto-scrolls to and highlights the current step during playback

### Step Telemetry

Each step carries performance and network metrics:

| Metric | Description |
|--------|-------------|
| CPU Usage | Process CPU utilization during step |
| Memory | Managed heap used/total (MB) |
| FPS | Render frames per second |
| Network Requests | HTTP request count during step |
| Network Response Bytes | Total response data |
| DOM Nodes | Active visual tree node count |

The right panel's telemetry tabs display:
- **Performance chart** — SkiaSharp-rendered timeline with memory (green) and CPU (orange) lines, scoped to the step's time window
- **Network waterfall** — Request timeline bars with method, URL, status code (color-coded), and duration

## Programmatic Access

### Check Frame Count

```json
{
  "id": 1,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "System.IO.Directory.GetFiles(((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.OutputDirectory + \"/images\", \"frame_*.jpg\").Length"
  }
}
```

### Enable Video via CDP

```json
{
  "id": 1,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.IsRecordVideoEnabled = true"
  }
}
```

## Performance Considerations

| Setting | Impact |
|---------|--------|
| Frame interval | Lower intervals produce smoother video but more I/O and larger output |
| Resolution | High-DPI displays produce larger frames; tiled transfer helps |
| Disk speed | Frame writes are asynchronous but fast storage helps |
| Test duration | Longer tests produce more frames |

:::tip
For CI/CD pipelines where video output is not needed for every run, disable video recording to reduce execution time and artifact size. Enable it only for failure investigation.
:::

## Integration with Test Reports

When both video recording and report generation are enabled:
- The HTML report includes a link to the video frames directory
- Step screenshots (from report generation) and video frames (from screencast) are stored in the same `images/` directory
- Step screenshots use `step_*_screenshot.png` naming while video frames use `frame_*.jpg` naming

## Next Steps

- [Test Reports](/articles/test-reports) — HTML and PDF report generation
- [Test Studio](/articles/test-studio) — Visual test editing workspace
- [Recorder Overview](/articles/recorder-overview) — Interaction capture architecture
