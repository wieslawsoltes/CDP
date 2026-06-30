#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SkiaSharp;

namespace Chrome.DevTools.Protocol;

public class NetworkTelemetryProvider : ITelemetryProvider
{
    public string Id => "Network";
    public string Name => "Network Waterfall";

    private readonly List<NetworkReportItem> _requests = new();
    private readonly object _lock = new();

    private int _stepRequestCount;
    private long _stepResponseBytes;

    public async Task InitializeAsync(ICdpService cdpService)
    {
        lock (_lock)
        {
            _requests.Clear();
        }
        try
        {
            await cdpService.SendCommandAsync("Network.enable");
        }
        catch { }
    }

    public void HandleEvent(string method, JsonNode? paramsNode)
    {
        // Centralized VM event processing already handles network and updates provider directly via helpers
    }

    public void OnStepStart(int stepIndex, double relativeStartMs)
    {
        _stepRequestCount = 0;
        _stepResponseBytes = 0;
    }

    public void OnStepEnd(int stepIndex, double relativeStartMs, double durationMs, ICdpService cdpService)
    {
        // Handled by VM capturing details
    }

    public void RecordRequest(NetworkReportItem item)
    {
        lock (_lock)
        {
            if (!_requests.Any(r => r.RequestId == item.RequestId))
            {
                _requests.Add(item);
            }
        }
    }

    public void UpdateResponse(string requestId, string status)
    {
        lock (_lock)
        {
            var existing = _requests.FirstOrDefault(r => r.RequestId == requestId);
            if (existing != null)
            {
                existing.Status = status;
            }
        }
    }

    public void FinishLoading(string requestId, long encodedLength, double durationMs)
    {
        lock (_lock)
        {
            var existing = _requests.FirstOrDefault(r => r.RequestId == requestId);
            if (existing != null)
            {
                existing.EncodedDataLength = encodedLength;
                existing.DurationMs = durationMs;
            }
        }
    }

    public void IncrementStepCount()
    {
        _stepRequestCount++;
    }

    public void AddStepBytes(long bytes)
    {
        _stepResponseBytes += bytes;
    }

    public void SetStepNetwork(int count, long bytes)
    {
        _stepRequestCount = count;
        _stepResponseBytes = bytes;
    }

    public JsonNode? CaptureStepData()
    {
        return new JsonObject
        {
            ["NetworkRequestCount"] = _stepRequestCount,
            ["NetworkResponseBytes"] = _stepResponseBytes
        };
    }

    public JsonNode? CaptureRunData()
    {
        var array = new JsonArray();
        lock (_lock)
        {
            foreach (var r in _requests)
            {
                array.Add(new JsonObject
                {
                    ["RequestId"] = r.RequestId,
                    ["Url"] = r.Url,
                    ["Method"] = r.Method,
                    ["Status"] = r.Status,
                    ["RelativeStartMs"] = r.RelativeStartMs,
                    ["DurationMs"] = r.DurationMs,
                    ["EncodedDataLength"] = r.EncodedDataLength
                });
            }
        }
        return array;
    }

    public string RenderHtml(int stepIndex, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options)
    {
        if (stepData == null) return "";

        int count = stepData["NetworkRequestCount"]?.GetValue<int>() ?? 0;
        long bytes = stepData["NetworkResponseBytes"]?.GetValue<long>() ?? 0;

        var html = "";

        if (options.IncludeMetricsTable)
        {
            html += $@"
                <div style=""margin-top: 0.5rem;"">
                    <table class=""metadata-table"">
                        <tr>
                            <td class=""label"">Network Traffic:</td>
                            <td class=""value"">{count} requests | {(bytes / 1024.0):F2} KB</td>
                        </tr>
                    </table>
                </div>";
        }

        if (options.IncludeNetworkDetails)
        {
            html += $@"
                <!-- Step Network Waterfall -->
                <div style=""margin-top: 1rem; border-top: 1px solid var(--border-color); padding-top: 1rem;"" id=""step-network-section-{stepIndex}"">
                    <h4 style=""font-size:0.9rem; color:var(--text-muted); margin-bottom:0.5rem;"">Step Network Waterfall</h4>
                    <div id=""step-waterfall-{stepIndex}"" style=""background: rgba(0,0,0,0.2); border: 1px solid var(--border-color); border-radius: 8px; padding: 0.75rem; font-family: monospace; font-size: 0.75rem; color: var(--text-main); overflow-x: auto;"">
                    </div>
                </div>";
        }

        return html;
    }

    public float GetRequiredPdfHeight(int stepIndex, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options)
    {
        if (!options.IncludeNetworkDetails || stepData == null) return 0f;
        
        // We need stepStartMs and durationMs to filter requests
        // Let's assume stepData contains them:
        double stepStartMs = stepData["RelativeStartMs"]?.GetValue<double>() ?? 0;
        double durationMs = stepData["DurationMs"]?.GetValue<double>() ?? 0;

        var requests = DeserializeRequests(runData);
        var stepStart = stepStartMs;
        var stepEnd = stepStartMs + durationMs;
        var stepReqs = requests.Where(r => r.RelativeStartMs <= stepEnd && (r.RelativeStartMs + r.DurationMs) >= stepStart).ToList();

        if (stepReqs.Count == 0) return 0f;

        float waterfallHeight = 20 + 10 * Math.Min(4, stepReqs.Count) + (stepReqs.Count > 4 ? 10 : 0);
        return waterfallHeight + 20f; // Add heading margin
    }

    public float RenderPdf(int stepIndex, SKCanvas canvas, SKRect bounds, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options)
    {
        if (!options.IncludeNetworkDetails || stepData == null) return 0f;

        double stepStartMs = stepData["RelativeStartMs"]?.GetValue<double>() ?? 0;
        double durationMs = stepData["DurationMs"]?.GetValue<double>() ?? 0;

        var requests = DeserializeRequests(runData);
        var stepStart = stepStartMs;
        var stepEnd = stepStartMs + durationMs;
        var stepReqs = requests.Where(r => r.RelativeStartMs <= stepEnd && (r.RelativeStartMs + r.DurationMs) >= stepStart).ToList();

        if (stepReqs.Count == 0) return 0f;

        float cx = bounds.Left + 15;
        float ty = bounds.Top + 10;
        float pageWidth = bounds.Right + 15; // margin is 15

        using var paintWfHeader = new SKPaint { TextSize = 8.5f, Color = new SKColor(37, 99, 235), IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        canvas.DrawText("Step Network Waterfall", cx, ty, paintWfHeader);
        ty += 12;

        float chartLeft = cx + 180;
        float chartWidth = bounds.Right - 15 - chartLeft;
        float chartHeight = 10 * Math.Min(4, stepReqs.Count) + (stepReqs.Count > 4 ? 10 : 0);

        using var paintWfBg = new SKPaint { Color = new SKColor(245, 246, 248), Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(chartLeft, ty - 2, chartLeft + chartWidth, ty + chartHeight), paintWfBg);
        using var paintWfBorder = new SKPaint { Color = new SKColor(218, 220, 224), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
        canvas.DrawRect(new SKRect(chartLeft, ty - 2, chartLeft + chartWidth, ty + chartHeight), paintWfBorder);

        canvas.DrawLine(chartLeft + chartWidth / 2f, ty - 2, chartLeft + chartWidth / 2f, ty + chartHeight, paintWfBorder);

        using var paintText = new SKPaint { TextSize = 7.5f, Color = new SKColor(95, 99, 104), IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Courier New") };
        using var paintUrl = new SKPaint { TextSize = 7f, Color = new SKColor(95, 99, 104), IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial") };
        using var paintPassed = new SKPaint { Color = new SKColor(16, 185, 129), TextSize = 7.5f, IsAntialias = true };
        using var paintFailed = new SKPaint { Color = new SKColor(239, 68, 68), TextSize = 7.5f, IsAntialias = true };
        using var paintWfBar = new SKPaint { Color = new SKColor(37, 99, 235), Style = SKPaintStyle.Fill };

        int drawLimit = Math.Min(4, stepReqs.Count);
        for (int rIdx = 0; rIdx < drawLimit; rIdx++)
        {
            var req = stepReqs[rIdx];

            string urlText = req.Url;
            try
            {
                var uri = new Uri(req.Url);
                urlText = uri.PathAndQuery;
                if (urlText.Length > 22) urlText = urlText.Substring(0, 20) + "..";
            }
            catch
            {
                if (urlText.Length > 22) urlText = urlText.Substring(0, 20) + "..";
            }

            string lineText = $"{req.Method,-6} {urlText,-24}";
            canvas.DrawText(lineText, cx, ty + 7, paintText);

            bool isPassed = req.Status.StartsWith("2") || req.Status.StartsWith("3") || req.Status.Equals("Finished", StringComparison.OrdinalIgnoreCase);
            var statusPaint = isPassed ? paintPassed : paintFailed;
            canvas.DrawText(req.Status, cx + 130, ty + 7, statusPaint);

            canvas.DrawText($"{req.DurationMs:F0} ms", cx + 185, ty + 7, paintUrl);

            // Draw waterfall timeline bar
            double reqStart = Math.Max(stepStart, req.RelativeStartMs);
            double reqEnd = Math.Min(stepEnd, req.RelativeStartMs + req.DurationMs);
            float barLeft = chartLeft + (float)((reqStart - stepStart) / durationMs * chartWidth);
            float barWidth = (float)((reqEnd - reqStart) / durationMs * chartWidth);
            if (barWidth < 1f) barWidth = 1f;

            canvas.DrawRect(new SKRect(barLeft, ty, barLeft + barWidth, ty + 6), paintWfBar);
            ty += 10;
        }

        if (stepReqs.Count > 4)
        {
            using var paintSub = new SKPaint { TextSize = 7f, Color = SKColors.Gray, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic) };
            canvas.DrawText($"+ {stepReqs.Count - 4} more network requests...", cx, ty + 7, paintSub);
            ty += 10;
        }

        return chartHeight + 20f;
    }

    private List<NetworkReportItem> DeserializeRequests(JsonNode? runData)
    {
        var list = new List<NetworkReportItem>();
        if (runData is JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node == null) continue;
                list.Add(new NetworkReportItem
                {
                    RequestId = node["RequestId"]?.GetValue<string>() ?? "",
                    Url = node["Url"]?.GetValue<string>() ?? "",
                    Method = node["Method"]?.GetValue<string>() ?? "",
                    Status = node["Status"]?.GetValue<string>() ?? "",
                    RelativeStartMs = node["RelativeStartMs"]?.GetValue<double>() ?? 0,
                    DurationMs = node["DurationMs"]?.GetValue<double>() ?? 0,
                    EncodedDataLength = node["EncodedDataLength"]?.GetValue<long>() ?? 0
                });
            }
        }
        return list;
    }
}
