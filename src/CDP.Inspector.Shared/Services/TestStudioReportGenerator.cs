using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace CdpInspectorApp.Services;

public class StepReportItem
{
    public int Index { get; set; }
    public string Action { get; set; } = "";
    public string ActionDisplay { get; set; } = "";
    public string Selector { get; set; } = "";
    public string Value { get; set; } = "";
    public string Status { get; set; } = ""; // Passed, Failed, Pending
    public double DurationMs { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string ScreenshotFileName { get; set; } = "";
    public string Url { get; set; } = "";
    public string ExtraMetadataJson { get; set; } = "{}";
    public double RelativeStartMs { get; set; }
}

public class VideoFrameItem
{
    public string FileName { get; set; } = "";
    public double TimestampMs { get; set; }
}

public static class TestStudioReportGenerator
{
    public static void GenerateHtmlReport(
        string outputFolder,
        string testName,
        string description,
        string appId,
        List<StepReportItem> steps,
        List<VideoFrameItem> videoFrames,
        DateTime startTime,
        DateTime endTime)
    {
        Directory.CreateDirectory(outputFolder);

        var encTestName = System.Net.WebUtility.HtmlEncode(testName ?? "");
        var encDescription = System.Net.WebUtility.HtmlEncode(description ?? "");
        var encAppId = System.Net.WebUtility.HtmlEncode(appId ?? "");

        var totalDuration = (endTime - startTime).TotalSeconds;
        var passedCount = steps.Count(s => s.Status.Equals("Passed", StringComparison.OrdinalIgnoreCase));
        var failedCount = steps.Count(s => s.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        var totalSteps = steps.Count;
        var successRate = totalSteps > 0 ? (int)Math.Round((double)passedCount / totalSteps * 100) : 0;
        var isAllPassed = passedCount == totalSteps && totalSteps > 0;
        var statusText = isAllPassed ? "PASSED" : "FAILED";
        var statusClass = isAllPassed ? "status-passed" : "status-failed";

        var stepsJson = JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = false });
        var framesJson = JsonSerializer.Serialize(videoFrames, new JsonSerializerOptions { WriteIndented = false });

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Test Run Report - {encTestName}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700&display=swap"" rel=""stylesheet"">
    <style>
        :root {{
            --bg-color: #0f1015;
            --panel-bg: rgba(22, 25, 35, 0.7);
            --border-color: rgba(255, 255, 255, 0.08);
            --text-main: #f3f4f6;
            --text-muted: #9ca3af;
            --primary: #2563eb;
            --primary-glow: rgba(37, 99, 235, 0.4);
            --passed: #10b981;
            --passed-glow: rgba(16, 185, 129, 0.25);
            --failed: #ef4444;
            --failed-glow: rgba(239, 68, 68, 0.25);
            --pending: #f59e0b;
        }}

        * {{
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }}

        body {{
            font-family: 'Outfit', -apple-system, BlinkMacSystemFont, sans-serif;
            background-color: var(--bg-color);
            color: var(--text-main);
            line-height: 1.5;
            padding: 2rem;
            min-height: 100vh;
        }}

        .container {{
            max-width: 1400px;
            margin: 0 auto;
        }}

        header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 2rem;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 1.5rem;
        }}

        .header-left h1 {{
            font-size: 2.2rem;
            font-weight: 700;
            background: linear-gradient(135deg, #fff 0%, #a5b4fc 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }}

        .header-left p {{
            color: var(--text-muted);
            margin-top: 0.25rem;
        }}

        .status-badge {{
            padding: 0.5rem 1.5rem;
            border-radius: 9999px;
            font-weight: 600;
            font-size: 1.1rem;
            letter-spacing: 0.05em;
            text-align: center;
            box-shadow: 0 0 20px var(--primary-glow);
        }}

        .status-passed {{
            background-color: var(--passed-glow);
            color: var(--passed);
            border: 1px solid var(--passed);
            box-shadow: 0 0 20px var(--passed-glow);
        }}

        .status-failed {{
            background-color: var(--failed-glow);
            color: var(--failed);
            border: 1px solid var(--failed);
            box-shadow: 0 0 20px var(--failed-glow);
        }}

        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 1.5rem;
            margin-bottom: 2rem;
        }}

        .stat-card {{
            background: var(--panel-bg);
            border: 1px solid var(--border-color);
            border-radius: 12px;
            padding: 1.5rem;
            backdrop-filter: blur(12px);
            display: flex;
            flex-direction: column;
            justify-content: center;
            transition: transform 0.2s ease, border-color 0.2s ease;
        }}

        .stat-card:hover {{
            transform: translateY(-2px);
            border-color: rgba(255, 255, 255, 0.15);
        }}

        .stat-card .label {{
            font-size: 0.85rem;
            color: var(--text-muted);
            text-transform: uppercase;
            letter-spacing: 0.05em;
            margin-bottom: 0.5rem;
        }}

        .stat-card .value {{
            font-size: 1.8rem;
            font-weight: 700;
        }}

        .content-grid {{
            display: grid;
            grid-template-columns: 1fr;
            gap: 2rem;
        }}

        @media (min-width: 1024px) {{
            .content-grid {{
                grid-template-columns: 1.6fr 1fr;
            }}
        }}

        .section-card {{
            background: var(--panel-bg);
            border: 1px solid var(--border-color);
            border-radius: 16px;
            padding: 1.5rem;
            backdrop-filter: blur(12px);
        }}

        .section-title {{
            font-size: 1.25rem;
            font-weight: 600;
            margin-bottom: 1.5rem;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }}

        /* Steps list styling */
        .steps-list {{
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }}

        .step-card {{
            border: 1px solid var(--border-color);
            border-radius: 10px;
            background: rgba(255, 255, 255, 0.02);
            overflow: hidden;
            transition: all 0.2s ease;
        }}

        .step-card.passed {{
            border-left: 4px solid var(--passed);
        }}

        .step-card.failed {{
            border-left: 4px solid var(--failed);
        }}

        .step-card.pending {{
            border-left: 4px solid var(--pending);
        }}

        .step-header {{
            padding: 1rem;
            display: flex;
            justify-content: space-between;
            align-items: center;
            cursor: pointer;
            user-select: none;
        }}

        .step-header:hover {{
            background: rgba(255, 255, 255, 0.04);
        }}

        .step-info {{
            display: flex;
            align-items: center;
            gap: 1rem;
        }}

        .step-index {{
            font-size: 0.9rem;
            background: rgba(255, 255, 255, 0.06);
            padding: 0.2rem 0.6rem;
            border-radius: 4px;
            font-weight: 500;
        }}

        .step-action {{
            font-weight: 600;
            color: #fff;
        }}

        .step-selector {{
            color: #8ab4f8;
            font-family: monospace;
            font-size: 0.9rem;
            background: rgba(138, 180, 248, 0.08);
            padding: 0.1rem 0.4rem;
            border-radius: 4px;
        }}

        .step-duration {{
            font-size: 0.85rem;
            color: var(--text-muted);
        }}

        .step-status-indicator {{
            width: 8px;
            height: 8px;
            border-radius: 50%;
        }}

        .passed .step-status-indicator {{ background-color: var(--passed); }}
        .failed .step-status-indicator {{ background-color: var(--failed); }}
        .pending .step-status-indicator {{ background-color: var(--pending); }}

        .step-body {{
            padding: 1rem;
            border-top: 1px solid var(--border-color);
            background: rgba(0, 0, 0, 0.15);
            display: none;
        }}

        .step-details-grid {{
            display: grid;
            grid-template-columns: 1fr;
            gap: 1.5rem;
        }}

        @media (min-width: 640px) {{
            .step-details-grid {{
                grid-template-columns: 1.2fr 1fr;
            }}
        }}

        .metadata-table {{
            width: 100%;
            border-collapse: collapse;
        }}

        .metadata-table td {{
            padding: 0.5rem 0;
            font-size: 0.9rem;
        }}

        .metadata-table td.label {{
            color: var(--text-muted);
            width: 35%;
            font-weight: 500;
        }}

        .metadata-table td.value {{
            font-family: monospace;
            word-break: break-all;
        }}

        .error-message {{
            background: rgba(239, 68, 68, 0.08);
            border: 1px solid rgba(239, 68, 68, 0.2);
            color: #f87171;
            padding: 0.75rem;
            border-radius: 6px;
            font-size: 0.85rem;
            margin-top: 0.5rem;
            font-family: monospace;
            white-space: pre-wrap;
        }}

        .step-screenshot-container {{
            display: flex;
            flex-direction: column;
            align-items: center;
        }}

        .step-screenshot {{
            max-width: 100%;
            max-height: 220px;
            border-radius: 6px;
            border: 1px solid var(--border-color);
            cursor: zoom-in;
            transition: opacity 0.2s;
        }}

        .step-screenshot:hover {{
            opacity: 0.85;
        }}

        /* Video Player Styling */
        .video-container {{
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 1rem;
            width: 100%;
        }}

        .player-canvas-wrapper {{
            position: relative;
            width: 100%;
            aspect-ratio: 16/9;
            background: #000;
            border-radius: 12px;
            border: 1px solid var(--border-color);
            overflow: hidden;
            display: flex;
            align-items: center;
            justify-content: center;
        }}

        #video-canvas {{
            max-width: 100%;
            max-height: 100%;
            object-fit: contain;
        }}

        .player-controls {{
            display: flex;
            flex-direction: column;
            width: 100%;
            gap: 0.75rem;
            background: rgba(255, 255, 255, 0.03);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 0.75rem;
        }}

        .slider-row {{
            display: flex;
            align-items: center;
            width: 100%;
            gap: 1rem;
        }}

        .seek-bar {{
            flex-grow: 1;
            height: 6px;
            border-radius: 3px;
            background: rgba(255, 255, 255, 0.1);
            outline: none;
            -webkit-appearance: none;
            cursor: pointer;
        }}

        .seek-bar::-webkit-slider-thumb {{
            -webkit-appearance: none;
            width: 14px;
            height: 14px;
            border-radius: 50%;
            background: var(--primary);
            box-shadow: 0 0 10px var(--primary-glow);
            transition: scale 0.1s ease;
        }}

        .seek-bar::-webkit-slider-thumb:hover {{
            scale: 1.25;
        }}

        .time-display {{
            font-size: 0.85rem;
            font-family: monospace;
            color: var(--text-muted);
            gap: 0.75rem;
            background: rgba(22, 25, 35, 0.4);
            border: 1px solid var(--border-color);
            padding: 1rem;
            border-radius: 12px;
        }}

        .controls-row {{
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 1rem;
        }}

        .controls-left {{
            display: flex;
            align-items: center;
            gap: 1rem;
        }}

        .play-btn {{
            background: var(--primary);
            color: #fff;
            border: none;
            padding: 0.4rem 1rem;
            border-radius: 6px;
            font-weight: 600;
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 0.35rem;
            font-size: 0.85rem;
            transition: all 0.2s ease;
        }}

        .play-btn:hover {{
            background: #3b82f6;
            box-shadow: 0 0 12px var(--primary-glow);
        }}

        .time-display {{
            font-size: 0.85rem;
            color: var(--text-muted);
            font-family: monospace;
        }}

        .seek-bar {{
            flex-grow: 1;
            accent-color: var(--primary);
            height: 4px;
            border-radius: 2px;
            cursor: pointer;
        }}

        .speed-control {{
            display: flex;
            align-items: center;
            gap: 0.25rem;
            font-size: 0.8rem;
            color: var(--text-muted);
        }}

        .speed-btn {{
            background: transparent;
            border: 1px solid var(--border-color);
            color: var(--text-muted);
            padding: 0.2rem 0.5rem;
            border-radius: 4px;
            cursor: pointer;
            font-weight: 500;
            font-size: 0.75rem;
            transition: all 0.15s ease;
        }}

        .speed-btn:hover, .speed-btn.active {{
            background: rgba(255, 255, 255, 0.05);
            color: #fff;
            border-color: rgba(255, 255, 255, 0.2);
        }}

        /* Modal styling */
        .modal {{
            display: none;
            position: fixed;
            z-index: 9999;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0, 0, 0, 0.9);
            align-items: center;
            justify-content: center;
            cursor: zoom-out;
            backdrop-filter: blur(8px);
        }}

        .modal-content {{
            max-width: 90%;
            max-height: 90%;
            border-radius: 8px;
            box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
            border: 1px solid rgba(255, 255, 255, 0.1);
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <header>
            <div class=""header-left"">
                <h1>{encTestName}</h1>
                <p>App ID: {encAppId} | {encDescription}</p>
            </div>
            <div class=""status-badge {statusClass}"">
                {statusText}
            </div>
        </header>

        <div class=""stats-grid"">
            <div class=""stat-card"">
                <span class=""label"">Total Steps</span>
                <span class=""value"">{totalSteps}</span>
            </div>
            <div class=""stat-card"">
                <span class=""label"">Passed</span>
                <span class=""value"" style=""color: var(--passed)"">{passedCount}</span>
            </div>
            <div class=""stat-card"">
                <span class=""label"">Failed</span>
                <span class=""value"" style=""color: var(--failed)"">{failedCount}</span>
            </div>
            <div class=""stat-card"">
                <span class=""label"">Success Rate</span>
                <span class=""value"" style=""color: {(failedCount > 0 ? "var(--failed)" : "var(--passed)")}"">{successRate}%</span>
            </div>
            <div class=""stat-card"">
                <span class=""label"">Duration</span>
                <span class=""value"">{totalDuration:F2}s</span>
            </div>
            <div class=""stat-card"">
                <span class=""label"">Date / Time</span>
                <span class=""value"" style=""font-size:1rem; font-weight:500; color:var(--text-muted)"">{startTime:yyyy-MM-dd HH:mm:ss}</span>
            </div>
        </div>

        <div class=""content-grid"">
            <!-- Left: Test Steps -->
            <div class=""section-card"">
                <h2 class=""section-title"">Execution Steps</h2>
                <div class=""steps-list"" id=""steps-list"">
                    <!-- Steps dynamically filled if needed, or static for stability -->
");
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var stepStatusClass = step.Status.ToLower();
            var hasSelector = !string.IsNullOrEmpty(step.Selector);
            var hasError = !string.IsNullOrEmpty(step.ErrorMessage);
            var hasScreenshot = !string.IsNullOrEmpty(step.ScreenshotFileName);

            var encActionDisplay = System.Net.WebUtility.HtmlEncode(step.ActionDisplay ?? "");
            var encSelector = System.Net.WebUtility.HtmlEncode(step.Selector ?? "");
            var encAction = System.Net.WebUtility.HtmlEncode(step.Action ?? "");
            var encValue = System.Net.WebUtility.HtmlEncode(step.Value ?? "");
            var encUrl = System.Net.WebUtility.HtmlEncode(step.Url ?? "");
            var encErrorMsg = System.Net.WebUtility.HtmlEncode(step.ErrorMessage ?? "");
            var encScreenshotFile = System.Net.WebUtility.HtmlEncode(step.ScreenshotFileName ?? "");

            sb.Append($@"                    <div class=""step-card {stepStatusClass}"" id=""step-card-{i}"">
                        <div class=""step-header"" onclick=""toggleStep({i})"">
                            <div class=""step-info"">
                                <div class=""step-status-indicator""></div>
                                <span class=""step-index"">#{i + 1}</span>
                                <span class=""step-action"">{encActionDisplay}</span>
                                {(hasSelector ? $"<span class=\"step-selector\">{encSelector}</span>" : "")}
                            </div>
                            <span class=""step-duration"">{(step.DurationMs):F0} ms</span>
                        </div>
                        <div class=""step-body"" id=""step-body-{i}"">
                            <div class=""step-details-grid"">
                                <div>
                                    <table class=""metadata-table"">
                                        <tr>
                                            <td class=""label"">Action:</td>
                                            <td class=""value"">{encAction}</td>
                                        </tr>
                                        {(hasSelector ? $"<tr><td class=\"label\">Selector:</td><td class=\"value\">{encSelector}</td></tr>" : "")}
                                        {(!string.IsNullOrEmpty(step.Value) ? $"<tr><td class=\"label\">Value:</td><td class=\"value\">{encValue}</td></tr>" : "")}
                                        {(!string.IsNullOrEmpty(step.Url) ? $"<tr><td class=\"label\">Active URL:</td><td class=\"value\"><a href=\"{encUrl}\" target=\"_blank\" style=\"color:#8ab4f8\">{encUrl}</a></td></tr>" : "")}
                                    </table>
                                    {(hasError ? $"<div class=\"error-message\">{encErrorMsg}</div>" : "")}
                                </div>
                                <div class=""step-screenshot-container"">
                                    {(hasScreenshot ? $@"<img src=""{encScreenshotFile}"" class=""step-screenshot"" onclick=""openModal('{encScreenshotFile}')"" alt=""Step Screenshot"">" : "<span style='color:var(--text-muted); font-size:0.9rem'>No screenshot captured</span>")}
                                </div>
                            </div>
                        </div>
                    </div>
");
        }

        sb.Append($@"                </div>
            </div>

            <!-- Right: Video Recording -->
            <div class=""section-card"">
                <h2 class=""section-title"">Video Recording</h2>
                <div class=""video-container"">
                    {(videoFrames.Count > 0 ? $@"
                    <div class=""player-canvas-wrapper"">
                        <canvas id=""video-canvas""></canvas>
                    </div>
                    <div class=""player-controls"">
                        <div class=""slider-row"">
                            <input type=""range"" class=""seek-bar"" id=""seek-bar"" min=""0"" max=""{videoFrames.Count - 1}"" value=""0"" step=""1"">
                            <span class=""time-display"" id=""time-display"">00:00 / 00:00</span>
                        </div>
                        <div class=""buttons-row"">
                            <button class=""play-btn"" id=""play-btn"">▶ PLAY</button>
                            <div class=""speed-control"">
                                <span>Speed:</span>
                                <button class=""speed-btn active"" onclick=""setSpeed(1)"">1x</button>
                                <button class=""speed-btn"" onclick=""setSpeed(2)"">2x</button>
                                <button class=""speed-btn"" onclick=""setSpeed(4)"">4x</button>
                            </div>
                        </div>
                    </div>" : @"
                    <div class=""player-canvas-wrapper"">
                        <div class=""no-video"">
                            <span>No video recorded for this run.</span>
                            <span style=""font-size:0.8rem; margin-top:0.5rem"">Enable video recording in options before starting execution.</span>
                        </div>
                    </div>")}
                </div>
            </div>
        </div>
    </div>

    <!-- Modal for screenshot zooming -->
    <div id=""screenshot-modal"" class=""modal"" onclick=""closeModal()"">
        <img class=""modal-content"" id=""modal-img"">
    </div>

    <script>
        // Data injected from backend
        const steps = {stepsJson};
        const frames = {framesJson};

        function toggleStep(index) {{
            const body = document.getElementById('step-body-' + index);
            if (body.style.display === 'block') {{
                body.style.display = 'none';
            }} else {{
                // Close all others
                const allBodies = document.querySelectorAll('.step-body');
                allBodies.forEach(b => b.style.display = 'none');
                body.style.display = 'block';
            }}
        }}

        function openModal(src) {{
            const modal = document.getElementById('screenshot-modal');
            const img = document.getElementById('modal-img');
            img.src = src;
            modal.style.display = 'flex';
        }}

        function closeModal() {{
            document.getElementById('screenshot-modal').style.display = 'none';
        }}

        // Video playback player logic
        if (frames.length > 0) {{
            const canvas = document.getElementById('video-canvas');
            const ctx = canvas.getContext('2d');
            const playBtn = document.getElementById('play-btn');
            const seekBar = document.getElementById('seek-bar');
            const timeDisplay = document.getElementById('time-display');
            
            let isPlaying = false;
            let currentFrameIndex = 0;
            let playbackSpeed = 1;
            let preloadedImages = [];
            let lastTickTime = 0;
            let playAccumulator = 0;

            // Preload images
            frames.forEach((f, idx) => {{
                const img = new Image();
                img.src = f.FileName;
                preloadedImages[idx] = img;
                if (idx === 0) {{
                    img.onload = () => {{
                        resizeCanvasToImage(img);
                        drawFrame(0);
                    }};
                }}
            }});

            function resizeCanvasToImage(img) {{
                canvas.width = img.naturalWidth || 800;
                canvas.height = img.naturalHeight || 600;
            }}

            function drawFrame(idx) {{
                if (idx < 0 || idx >= frames.length) return;
                const img = preloadedImages[idx];
                if (img && img.complete) {{
                    ctx.clearRect(0, 0, canvas.width, canvas.height);
                    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                }}
                seekBar.value = idx;
                updateTimeDisplay(idx);
            }}

            function updateTimeDisplay(idx) {{
                if (idx < 0 || idx >= frames.length) return;
                const curMs = frames[idx].TimestampMs;
                const totalMs = frames[frames.length - 1].TimestampMs;
                timeDisplay.innerText = formatTime(curMs) + ' / ' + formatTime(totalMs);
            }}

            function formatTime(ms) {{
                const totalSec = Math.floor(ms / 1000);
                const minutes = Math.floor(totalSec / 60);
                const seconds = totalSec % 60;
                const msRemain = Math.floor((ms % 1000) / 100);
                return String(minutes).padStart(2, '0') + ':' + String(seconds).padStart(2, '0') + '.' + msRemain;
            }}

            function playLoop(timestamp) {{
                if (!isPlaying) return;
                if (!lastTickTime) lastTickTime = timestamp;
                
                const delta = (timestamp - lastTickTime) * playbackSpeed;
                lastTickTime = timestamp;

                playAccumulator += delta;

                // Move frames forward according to timestamp differences
                let nextFrameIndex = currentFrameIndex;
                while (nextFrameIndex < frames.length - 1) {{
                    const curFrameTime = frames[nextFrameIndex].TimestampMs;
                    const nextFrameTime = frames[nextFrameIndex + 1].TimestampMs;
                    const diff = nextFrameTime - curFrameTime;
                    if (playAccumulator >= diff) {{
                        playAccumulator -= diff;
                        nextFrameIndex++;
                    }} else {{
                        break;
                    }}
                }}

                if (nextFrameIndex !== currentFrameIndex) {{
                    currentFrameIndex = nextFrameIndex;
                    drawFrame(currentFrameIndex);
                }}

                if (currentFrameIndex >= frames.length - 1) {{
                    isPlaying = false;
                    playBtn.innerText = '▶ PLAY';
                    currentFrameIndex = 0;
                    playAccumulator = 0;
                    lastTickTime = 0;
                }} else {{
                    requestAnimationFrame(playLoop);
                }}
            }}

            playBtn.addEventListener('click', () => {{
                if (isPlaying) {{
                    isPlaying = false;
                    playBtn.innerText = '▶ PLAY';
                }} else {{
                    isPlaying = true;
                    playBtn.innerText = '⏸ PAUSE';
                    lastTickTime = 0;
                    if (currentFrameIndex >= frames.length - 1) {{
                        currentFrameIndex = 0;
                    }}
                    requestAnimationFrame(playLoop);
                }}
            }});

            seekBar.addEventListener('input', (e) => {{
                currentFrameIndex = parseInt(e.target.value);
                drawFrame(currentFrameIndex);
                playAccumulator = 0;
                if (isPlaying) {{
                    lastTickTime = 0;
                }}
            }});

            window.setSpeed = function(speed) {{
                playbackSpeed = speed;
                const buttons = document.querySelectorAll('.speed-btn');
                buttons.forEach(btn => btn.classList.remove('active'));
                
                event.target.classList.add('active');
            }};
        }}
    </script>
</body>
</html>
");

        File.WriteAllText(Path.Combine(outputFolder, "index.html"), sb.ToString());
    }

    public static void GeneratePdfReport(string pdfPath, string testName, List<StepReportItem> steps)
    {
        using var stream = File.Create(pdfPath);
        using var document = SKDocument.CreatePdf(stream);

        // A4 Paper sizes in points: 595 x 842
        float pageWidth = 595;
        float pageHeight = 842;
        float margin = 40;

        using var paintText = new SKPaint
        {
            TextSize = 10,
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
        };

        using var paintBold = new SKPaint
        {
            TextSize = 11,
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        using var paintTitle = new SKPaint
        {
            TextSize = 22,
            Color = new SKColor(37, 99, 235), // Primary blue
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        using var paintSub = new SKPaint
        {
            TextSize = 10,
            Color = SKColors.Gray,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic)
        };

        using var paintPassed = new SKPaint
        {
            TextSize = 10,
            Color = new SKColor(16, 185, 129), // Emerald
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        using var paintFailed = new SKPaint
        {
            TextSize = 10,
            Color = new SKColor(239, 68, 68), // Red
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        using var paintCardBg = new SKPaint
        {
            Color = new SKColor(249, 250, 251),
            Style = SKPaintStyle.Fill
        };

        using var paintCardBorder = new SKPaint
        {
            Color = new SKColor(229, 231, 235),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        int pageNum = 1;
        float y = margin + 20;

        void DrawHeader(SKCanvas canvas)
        {
            canvas.DrawText("Test Run Report", margin, y, paintTitle);
            y += 28;
            canvas.DrawText($"Test Target: {testName}", margin, y, paintBold);
            y += 16;
            canvas.DrawText($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", margin, y, paintSub);
            y += 20;

            // Draw line separator
            using var linePaint = new SKPaint { Color = new SKColor(229, 231, 235), StrokeWidth = 1 };
            canvas.DrawLine(margin, y, pageWidth - margin, y, linePaint);
            y += 25;
        }

        void DrawFooter(SKCanvas canvas, int page)
        {
            var text = $"Page {page}";
            float x = pageWidth - margin - paintSub.MeasureText(text);
            canvas.DrawText(text, x, pageHeight - 20, paintSub);
            canvas.DrawText("Test Studio Runner | CDP Inspector Client", margin, pageHeight - 20, paintSub);
        }

        SKCanvas? currentCanvas = document.BeginPage(pageWidth, pageHeight);
        if (currentCanvas == null) return;

        DrawHeader(currentCanvas);

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            
            // Check remaining space
            float requiredHeight = 110;
            bool hasScreenshot = false;
            SKBitmap? screenshotBitmap = null;
            if (!string.IsNullOrEmpty(step.ScreenshotFileName))
            {
                // Check if it is base64
                if (!step.ScreenshotFileName.Contains("/") && !step.ScreenshotFileName.Contains("\\") && step.ScreenshotFileName.Length > 100)
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(step.ScreenshotFileName);
                        screenshotBitmap = SKBitmap.Decode(bytes);
                        hasScreenshot = screenshotBitmap != null;
                    }
                    catch { }
                }
                else
                {
                    var reportDir = Path.GetDirectoryName(pdfPath);
                    var fullPath = Path.IsPathRooted(step.ScreenshotFileName)
                        ? step.ScreenshotFileName
                        : Path.Combine(reportDir ?? "", step.ScreenshotFileName);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            screenshotBitmap = SKBitmap.Decode(fullPath);
                            hasScreenshot = screenshotBitmap != null;
                        }
                        catch { }
                    }
                }
            }

            if (hasScreenshot && screenshotBitmap != null)
            {
                requiredHeight += 120;
            }

            if (y + requiredHeight > pageHeight - margin - 40)
            {
                DrawFooter(currentCanvas, pageNum);
                document.EndPage();

                pageNum++;
                currentCanvas = document.BeginPage(pageWidth, pageHeight);
                y = margin + 20;
                DrawHeader(currentCanvas);
            }

            // Draw Step Rounded Card container
            var cardTop = y;
            var cardHeight = requiredHeight - 10;
            var cardRect = new SKRect(margin, cardTop, pageWidth - margin, cardTop + cardHeight);
            currentCanvas.DrawRoundRect(cardRect, 6, 6, paintCardBg);
            currentCanvas.DrawRoundRect(cardRect, 6, 6, paintCardBorder);

            float cx = margin + 15;
            float cy = y + 20;

            // Step Header Line
            currentCanvas.DrawText($"#{step.Index}  {step.ActionDisplay}", cx, cy, paintBold);

            // Draw status
            bool isPassed = step.Status.Equals("Passed", StringComparison.OrdinalIgnoreCase);
            var statusPaint = isPassed ? paintPassed : paintFailed;
            float statusX = pageWidth - margin - 15 - statusPaint.MeasureText(step.Status.ToUpper());
            currentCanvas.DrawText(step.Status.ToUpper(), statusX, cy, statusPaint);

            cy += 18;

            // Details
            if (!string.IsNullOrEmpty(step.Selector))
            {
                currentCanvas.DrawText($"Selector: {step.Selector}", cx, cy, paintText);
                cy += 14;
            }
            if (!string.IsNullOrEmpty(step.Value))
            {
                currentCanvas.DrawText($"Value: {step.Value}", cx, cy, paintText);
                cy += 14;
            }
            if (!string.IsNullOrEmpty(step.Url))
            {
                currentCanvas.DrawText($"URL: {step.Url}", cx, cy, paintText);
                cy += 14;
            }
            currentCanvas.DrawText($"Duration: {step.DurationMs:F0} ms", cx, cy, paintText);

            if (!string.IsNullOrEmpty(step.ErrorMessage))
            {
                cy += 14;
                using var errPaint = new SKPaint { TextSize = 9, Color = SKColors.Red, IsAntialias = true };
                currentCanvas.DrawText($"Error: {step.ErrorMessage}", cx, cy, errPaint);
            }

            if (hasScreenshot && screenshotBitmap != null)
            {
                cy += 15;
                try
                {
                    // Scale bitmap to fit
                    float maxImgW = pageWidth - margin * 2 - 30;
                    float maxImgH = 100;
                    float scale = Math.Min(maxImgW / screenshotBitmap.Width, maxImgH / screenshotBitmap.Height);
                    float imgW = screenshotBitmap.Width * scale;
                    float imgH = screenshotBitmap.Height * scale;

                    // Center horizontally
                    float imgX = margin + (pageWidth - margin * 2 - imgW) / 2.0f;
                    var imgRect = new SKRect(imgX, cy, imgX + imgW, cy + imgH);
                    currentCanvas.DrawBitmap(screenshotBitmap, imgRect);
                }
                catch (Exception)
                {
                    // Ignore drawing failed images
                }
                finally
                {
                    screenshotBitmap.Dispose();
                }
            }

            y += requiredHeight;
        }

        DrawFooter(currentCanvas, pageNum);
        document.EndPage();
        document.Close();
    }
}
