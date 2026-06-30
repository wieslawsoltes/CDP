#nullable enable

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SkiaSharp;

namespace Chrome.DevTools.Protocol;

public interface ITelemetryProvider
{
    string Id { get; }
    string Name { get; }

    Task InitializeAsync(ICdpService cdpService);
    void HandleEvent(string method, JsonNode? paramsNode);

    void OnStepStart(int stepIndex, double relativeStartMs);
    void OnStepEnd(int stepIndex, double relativeStartMs, double durationMs, ICdpService cdpService);

    JsonNode? CaptureStepData();
    JsonNode? CaptureRunData();

    string RenderHtml(int stepIndex, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options);
    
    float GetRequiredPdfHeight(int stepIndex, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options);
    float RenderPdf(int stepIndex, SKCanvas canvas, SKRect bounds, JsonNode? stepData, JsonNode? runData, TestStudioReportOptions options);
}

public static class TelemetryRegistry
{
    private static readonly List<ITelemetryProvider> _providers = new();

    static TelemetryRegistry()
    {
        Register(new PerformanceTelemetryProvider());
        Register(new NetworkTelemetryProvider());
    }

    public static IReadOnlyList<ITelemetryProvider> Providers => _providers;

    public static void Register(ITelemetryProvider provider)
    {
        if (provider == null) return;
        if (!_providers.Exists(p => p.Id == provider.Id))
        {
            _providers.Add(provider);
        }
    }

    public static void Unregister(string id)
    {
        _providers.RemoveAll(p => p.Id == id);
    }

    public static void Clear()
    {
        _providers.Clear();
    }
}
