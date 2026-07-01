#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SkiaSharp;

namespace Chrome.DevTools.Protocol;

public class PerformanceTelemetryProvider : ITelemetryProvider
{
    public string Id => "Performance";
    public string Name => "Performance";

    private readonly List<RunMetricSample> _samples = new();
    private readonly object _lock = new();

    private double _stepCpuUsage;
    private double _stepMemoryJsHeapUsed;
    private double _stepMemoryJsHeapTotal;
    private double _stepFps;
    private int _stepDomNodes;
    private int _stepDomDocuments;

    public async Task InitializeAsync(ICdpService cdpService)
    {
        lock (_lock)
        {
            _samples.Clear();
        }
        try
        {
            await cdpService.SendCommandAsync("Performance.enable");
        }
        catch { }
    }

    public void HandleEvent(string method, JsonNode? paramsNode)
    {
        if (method == "Performance.metrics" && paramsNode != null)
        {
            try
            {
                var metrics = paramsNode["metrics"] as JsonArray;
                if (metrics != null)
                {
                    double cpu = 0;
                    double memory = 0;
                    double fps = 0;
                    foreach (var m in metrics)
                    {
                        string name = m?["name"]?.GetValue<string>() ?? "";
                        double val = GetDouble(m?["value"]);
                        if (name == "CPUUsage") cpu = val;
                        else if (name == "JSHeapUsedSize") memory = val / 1024.0 / 1024.0; // MB
                        else if (name == "FPS") fps = val;
                    }

                    // We will capture timestamp relatively, but wait! TestStudioViewModel tracks startTime.
                    // To keep it simple, the VM can manage adding samples directly to the provider timeline
                    // or the provider can do it. For now, since the VM runs events inside a centralized context,
                    // we will let the VM add to the provider timeline or collect it dynamically.
                }
            }
            catch { }
        }
    }

    public void OnStepStart(int stepIndex, double relativeStartMs)
    {
        _stepCpuUsage = 0;
        _stepMemoryJsHeapUsed = 0;
        _stepMemoryJsHeapTotal = 0;
        _stepFps = 0;
        _stepDomNodes = 0;
        _stepDomDocuments = 0;
    }

    public async void OnStepEnd(int stepIndex, double relativeStartMs, double durationMs, ICdpService cdpService)
    {
        try
        {
            var perfRes = await cdpService.SendCommandAsync("Performance.getMetrics");
            if (perfRes != null)
            {
                var metrics = perfRes["metrics"] as JsonArray;
                if (metrics != null)
                {
                    foreach (var m in metrics)
                    {
                        string name = m?["name"]?.GetValue<string>() ?? "";
                        double val = GetDouble(m?["value"]);
                        if (name == "Nodes") _stepDomNodes = (int)val;
                        else if (name == "JSHeapUsedSize") _stepMemoryJsHeapUsed = val / 1024.0 / 1024.0;
                        else if (name == "JSHeapTotalSize") _stepMemoryJsHeapTotal = val / 1024.0 / 1024.0;
                        else if (name == "CPUUsage") _stepCpuUsage = val;
                        else if (name == "FPS") _stepFps = val;
                    }
                }
            }

            var memRes = await cdpService.SendCommandAsync("Memory.getDOMCounters");
            if (memRes != null)
            {
                _stepDomDocuments = memRes["documents"]?.GetValue<int>() ?? 0;
            }

            // Push a sample at step end
            lock (_lock)
            {
                _samples.Add(new RunMetricSample
                {
                    RelativeTimeMs = relativeStartMs + durationMs,
                    CpuUsage = _stepCpuUsage,
                    MemoryJsHeapUsed = _stepMemoryJsHeapUsed,
                    Fps = _stepFps
                });
            }
        }
        catch { }
    }

    public void AddMetricSample(RunMetricSample sample)
    {
        lock (_lock)
        {
            _samples.Add(sample);
        }
    }

    public void SetStepMetrics(double cpu, double memUsed, double memTotal, double fps, int nodes, int docs)
    {
        _stepCpuUsage = cpu;
        _stepMemoryJsHeapUsed = memUsed;
        _stepMemoryJsHeapTotal = memTotal;
        _stepFps = fps;
        _stepDomNodes = nodes;
        _stepDomDocuments = docs;
    }

    public JsonNode? CaptureStepData()
    {
        return new JsonObject
        {
            ["CpuUsage"] = _stepCpuUsage,
            ["MemoryJsHeapUsed"] = _stepMemoryJsHeapUsed,
            ["MemoryJsHeapTotal"] = _stepMemoryJsHeapTotal,
            ["Fps"] = _stepFps,
            ["DomNodes"] = _stepDomNodes,
            ["DomDocuments"] = _stepDomDocuments
        };
    }

    public JsonNode? CaptureRunData()
    {
        var array = new JsonArray();
        lock (_lock)
        {
            foreach (var s in _samples)
            {
                array.Add(new JsonObject
                {
                    ["RelativeTimeMs"] = s.RelativeTimeMs,
                    ["CpuUsage"] = s.CpuUsage,
                    ["MemoryJsHeapUsed"] = s.MemoryJsHeapUsed,
                    ["Fps"] = s.Fps
                });
            }
        }
        return array;
    }

    public string RenderHtml(int stepIndex, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options)
    {
        if (stepData == null) return "";

        double cpu = GetDouble(stepData["CpuUsage"]);
        double jsUsed = GetDouble(stepData["MemoryJsHeapUsed"]);
        double jsTotal = GetDouble(stepData["MemoryJsHeapTotal"]);
        double fps = GetDouble(stepData["Fps"]);
        int domNodes = (int)GetDouble(stepData["DomNodes"]);
        int domDocs = (int)GetDouble(stepData["DomDocuments"]);

        var html = "";

        if (options.IncludeMetricsTable)
        {
            html += $@"
                <div style=""margin-top: 1rem; border-top: 1px solid var(--border-color); padding-top: 0.75rem;"">
                    <h4 style=""font-size:0.9rem; color:var(--primary); margin-bottom:0.5rem;"">Step Performance Metrics</h4>
                    <table class=""metadata-table"">
                        <tr>
                            <td class=""label"">CPU Usage:</td>
                            <td class=""value"">{cpu:F1} %</td>
                        </tr>
                        <tr>
                            <td class=""label"">JS Heap Used:</td>
                            <td class=""value"">{jsUsed:F2} MB</td>
                        </tr>
                        <tr>
                            <td class=""label"">JS Heap Total:</td>
                            <td class=""value"">{jsTotal:F2} MB</td>
                        </tr>
                        <tr>
                            <td class=""label"">FPS:</td>
                            <td class=""value"">{fps:F1} FPS</td>
                        </tr>
                        <tr>
                            <td class=""label"">DOM Nodes / Docs:</td>
                            <td class=""value"">{domNodes} / {domDocs}</td>
                        </tr>
                    </table>
                </div>";
        }

        if (options.IncludeCharts)
        {
            html += $@"
                <!-- Step Performance Chart -->
                <div style=""margin-top: 1rem; border-top: 1px solid var(--border-color); padding-top: 1rem;"">
                    <h4 style=""font-size:0.9rem; color:var(--text-muted); margin-bottom:0.5rem;"">Performance Timeline Highlight</h4>
                    <div style=""height: 120px; background: rgba(0,0,0,0.2); border: 1px solid var(--border-color); border-radius: 8px; position: relative; overflow: hidden;"">
                        <svg id=""step-chart-{stepIndex}"" style=""width:100%; height:100%;""></svg>
                    </div>
                </div>";
        }

        return html;
    }

    public float GetRequiredPdfHeight(int stepIndex, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options)
    {
        bool drawMetricsRow = options.IncludeMetricsTable || (options.IncludeCharts && runData is JsonArray arr && arr.Count > 0);
        return drawMetricsRow ? 80f : 0f;
    }

    public float RenderPdf(int stepIndex, SKCanvas canvas, SKRect bounds, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options)
    {
        bool drawMetricsRow = options.IncludeMetricsTable || (options.IncludeCharts && runData is JsonArray arr && arr.Count > 0);
        if (!drawMetricsRow) return 0f;

        float cx = bounds.Left + 15;
        float rowTop = bounds.Top;

        using var paintText = new SKPaint { TextSize = 8, Color = new SKColor(95, 99, 104), IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial") };

        // Draw metrics table on the left
        if (options.IncludeMetricsTable && stepData != null)
        {
            double cpu = GetDouble(stepData["CpuUsage"]);
            double jsUsed = GetDouble(stepData["MemoryJsHeapUsed"]);
            double jsTotal = GetDouble(stepData["MemoryJsHeapTotal"]);
            double fps = GetDouble(stepData["Fps"]);
            int domNodes = (int)GetDouble(stepData["DomNodes"]);
            int domDocs = (int)GetDouble(stepData["DomDocuments"]);

            float tx = cx;
            float ty = rowTop + 10;
            
            using var paintMetricsHeader = new SKPaint { TextSize = 8.5f, Color = new SKColor(37, 99, 235), IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
            canvas.DrawText("Step Performance Metrics", tx, ty, paintMetricsHeader);
            ty += 12;

            canvas.DrawText($"CPU: {cpu:F1} %  |  FPS: {fps:F1}", tx, ty, paintText);
            ty += 11;
            canvas.DrawText($"Memory: {jsUsed:F2} / {jsTotal:F2} MB", tx, ty, paintText);
            ty += 11;
            canvas.DrawText($"DOM: {domNodes} nodes / {domDocs} docs", tx, ty, paintText);
        }

        // Draw mini-chart on the right
        var samples = DeserializeTimeline(runData);
        if (options.IncludeCharts && samples.Count > 0 && stepData != null)
        {
            // We need relativeStartMs and durationMs, which can be extracted from stepData? Wait, they are properties of StepReportItem, but we can pass them or fetch them.
            // Let's assume stepData contains them:
            // Wait, does stepData have relativeStartMs and durationMs? We can save them in CaptureStepData!
            // But let's check:
            // StepReportItem has DurationMs and RelativeStartMs. Let's make sure our CaptureStepData saves them!
            double relativeStartMs = GetDouble(stepData["RelativeStartMs"]);
            double durationMs = GetDouble(stepData["DurationMs"]);

            float chartX = bounds.Right - 250;
            float chartY = rowTop + 2;
            var chartRect = new SKRect(chartX, chartY, bounds.Right - 15, chartY + 68);
            
            DrawMiniPerformanceChart(canvas, chartRect, samples, relativeStartMs, durationMs);
        }

        return 80f;
    }

    private List<RunMetricSample> DeserializeTimeline(JsonNode? runData)
    {
        var list = new List<RunMetricSample>();
        if (runData is JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node == null) continue;
                list.Add(new RunMetricSample
                {
                    RelativeTimeMs = GetDouble(node["RelativeTimeMs"]),
                    CpuUsage = GetDouble(node["CpuUsage"]),
                    MemoryJsHeapUsed = GetDouble(node["MemoryJsHeapUsed"]),
                    Fps = GetDouble(node["Fps"])
                });
            }
        }
        return list;
    }

    private void DrawMiniPerformanceChart(SKCanvas canvas, SKRect rect, List<RunMetricSample> samples, double stepStartMs, double stepDurationMs)
    {
        using var paintBg = new SKPaint { Color = new SKColor(245, 246, 248), Style = SKPaintStyle.Fill };
        canvas.DrawRect(rect, paintBg);

        using var paintBorder = new SKPaint { Color = new SKColor(218, 220, 224), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(rect, paintBorder);

        if (samples.Count < 2)
        {
            using var paintNoData = new SKPaint { Color = SKColors.Gray, TextSize = 8, IsAntialias = true };
            canvas.DrawText("No timeline data", rect.Left + 10, rect.Top + 20, paintNoData);
            return;
        }

        double maxTime = samples[samples.Count - 1].RelativeTimeMs;
        if (maxTime <= 0) maxTime = 1;

        double maxCpu = 100;
        foreach (var s in samples) if (s.CpuUsage > maxCpu) maxCpu = s.CpuUsage;

        double maxMem = 128;
        foreach (var s in samples) if (s.MemoryJsHeapUsed > maxMem) maxMem = s.MemoryJsHeapUsed;

        float highlightLeft = rect.Left + (float)(stepStartMs / maxTime * rect.Width);
        float highlightRight = rect.Left + (float)((stepStartMs + stepDurationMs) / maxTime * rect.Width);
        highlightLeft = Math.Max(rect.Left, highlightLeft);
        highlightRight = Math.Min(rect.Right, highlightRight);

        if (highlightRight > highlightLeft)
        {
            using var paintHighlight = new SKPaint { Color = new SKColor(37, 99, 235, 30), Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(highlightLeft, rect.Top, highlightRight, rect.Bottom), paintHighlight);

            using var paintLine = new SKPaint { Color = new SKColor(37, 99, 235, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            canvas.DrawLine(highlightLeft, rect.Top, highlightLeft, rect.Bottom, paintLine);
        }

        using var pathCpu = new SKPath();
        using var pathMem = new SKPath();

        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            float x = rect.Left + (float)(sample.RelativeTimeMs / maxTime * rect.Width);
            float yCpu = rect.Bottom - (float)(sample.CpuUsage / maxCpu * rect.Height);
            float yMem = rect.Bottom - (float)(sample.MemoryJsHeapUsed / maxMem * rect.Height);

            x = Math.Max(rect.Left, Math.Min(rect.Right, x));
            yCpu = Math.Max(rect.Top, Math.Min(rect.Bottom, yCpu));
            yMem = Math.Max(rect.Top, Math.Min(rect.Bottom, yMem));

            if (i == 0)
            {
                pathCpu.MoveTo(x, yCpu);
                pathMem.MoveTo(x, yMem);
            }
            else
            {
                pathCpu.LineTo(x, yCpu);
                pathMem.LineTo(x, yMem);
            }
        }

        using var paintCpu = new SKPaint { Color = new SKColor(245, 158, 11), Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = true };
        using var paintMem = new SKPaint { Color = new SKColor(16, 185, 129), Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = true };

        canvas.DrawPath(pathCpu, paintCpu);
        canvas.DrawPath(pathMem, paintMem);

        using var paintLegend = new SKPaint { TextSize = 6.5f, IsAntialias = true };
        paintLegend.Color = new SKColor(245, 158, 11);
        canvas.DrawText("CPU", rect.Left + 5, rect.Top + 8, paintLegend);
        paintLegend.Color = new SKColor(16, 185, 129);
        canvas.DrawText("Memory", rect.Left + 30, rect.Top + 8, paintLegend);
    }

    private static double GetDouble(JsonNode? node)
    {
        if (node == null) return 0.0;
        if (node is JsonValue jsonVal)
        {
            if (jsonVal.TryGetValue<double>(out double d)) return d;
            if (jsonVal.TryGetValue<int>(out int i)) return i;
            if (jsonVal.TryGetValue<long>(out long l)) return l;
            if (jsonVal.TryGetValue<float>(out float f)) return f;
        }
        return 0.0;
    }
}
