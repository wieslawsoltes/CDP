#nullable enable

using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Services;

public interface ITelemetryChartService
{
    Bitmap? RenderPerformanceChart(List<RunMetricSample> samples, double relativeStartMs, double durationMs, int width = 800, int height = 150);
}
