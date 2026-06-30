#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.Views;

public partial class StepTelemetryView : UserControl
{
    public static readonly StyledProperty<StepReportItem?> SelectedStepProperty =
        AvaloniaProperty.Register<StepTelemetryView, StepReportItem?>(nameof(SelectedStep));

    public static readonly StyledProperty<List<RunMetricSample>?> MetricsProperty =
        AvaloniaProperty.Register<StepTelemetryView, List<RunMetricSample>?>(nameof(Metrics));

    public static readonly StyledProperty<List<NetworkReportItem>?> NetworkProperty =
        AvaloniaProperty.Register<StepTelemetryView, List<NetworkReportItem>?>(nameof(Network));

    public static readonly StyledProperty<ITelemetryChartService?> ChartServiceProperty =
        AvaloniaProperty.Register<StepTelemetryView, ITelemetryChartService?>(nameof(ChartService));

    public StepReportItem? SelectedStep
    {
        get => GetValue(SelectedStepProperty);
        set => SetValue(SelectedStepProperty, value);
    }

    public List<RunMetricSample>? Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public List<NetworkReportItem>? Network
    {
        get => GetValue(NetworkProperty);
        set => SetValue(NetworkProperty, value);
    }

    public ITelemetryChartService? ChartService
    {
        get => GetValue(ChartServiceProperty);
        set => SetValue(ChartServiceProperty, value);
    }

    static StepTelemetryView()
    {
        SelectedStepProperty.Changed.AddClassHandler<StepTelemetryView>((x, e) => x.UpdateDetails());
        MetricsProperty.Changed.AddClassHandler<StepTelemetryView>((x, e) => x.UpdateDetails());
        NetworkProperty.Changed.AddClassHandler<StepTelemetryView>((x, e) => x.UpdateDetails());
        ChartServiceProperty.Changed.AddClassHandler<StepTelemetryView>((x, e) => x.UpdateDetails());
    }

    public StepTelemetryView()
    {
        InitializeComponent();
        this.SizeChanged += (s, e) => UpdateDetails();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateDetails()
    {
        var step = SelectedStep;
        var metrics = Metrics;
        var network = Network;

        var tabControl = this.FindControl<TabControl>("tabTelemetry");
        if (tabControl == null) return;

        if (step == null)
        {
            tabControl.Items.Clear();
            return;
        }

        // Initialize tabs if empty or count mismatch
        if (tabControl.Items.Count != TelemetryUiRegistry.UiProviders.Count)
        {
            tabControl.Items.Clear();
            foreach (var uiProvider in TelemetryUiRegistry.UiProviders)
            {
                var stepData = GetStepData(uiProvider.ProviderId, step);
                var runData = GetRunData(uiProvider.ProviderId, metrics, network);

                var view = uiProvider.CreateView(stepData, runData);
                var tabItem = new TabItem
                {
                    Header = uiProvider.Name,
                    Content = view
                };
                tabControl.Items.Add(tabItem);
            }
        }
        else
        {
            // Just update views
            for (int i = 0; i < TelemetryUiRegistry.UiProviders.Count; i++)
            {
                var uiProvider = TelemetryUiRegistry.UiProviders[i];
                var tabItem = tabControl.Items[i] as TabItem;
                var view = tabItem?.Content as Control;
                if (view != null)
                {
                    var stepData = GetStepData(uiProvider.ProviderId, step);
                    var runData = GetRunData(uiProvider.ProviderId, metrics, network);
                    uiProvider.UpdateView(view, stepData, runData);
                }
            }
        }
    }

    private JsonNode? GetStepData(string providerId, StepReportItem step)
    {
        if (step.Telemetry != null && step.Telemetry.TryGetValue(providerId, out var stepData))
        {
            return stepData;
        }

        // Fallback for legacy data
        if (providerId == "Performance")
        {
            return new JsonObject
            {
                ["CpuUsage"] = step.CpuUsage,
                ["MemoryJsHeapUsed"] = step.MemoryJsHeapUsed,
                ["MemoryJsHeapTotal"] = step.MemoryJsHeapTotal,
                ["Fps"] = step.Fps,
                ["DomNodes"] = step.DomNodes,
                ["DomDocuments"] = step.DomDocuments,
                ["RelativeStartMs"] = step.RelativeStartMs,
                ["DurationMs"] = step.DurationMs
            };
        }
        else if (providerId == "Network")
        {
            return new JsonObject
            {
                ["NetworkRequestCount"] = step.NetworkRequestCount,
                ["NetworkResponseBytes"] = step.NetworkResponseBytes,
                ["RelativeStartMs"] = step.RelativeStartMs,
                ["DurationMs"] = step.DurationMs
            };
        }

        return null;
    }

    private JsonNode? GetRunData(string providerId, List<RunMetricSample>? metrics, List<NetworkReportItem>? network)
    {
        if (providerId == "Performance" && metrics != null)
        {
            var arr = new JsonArray();
            foreach (var s in metrics)
            {
                arr.Add(new JsonObject
                {
                    ["RelativeTimeMs"] = s.RelativeTimeMs,
                    ["CpuUsage"] = s.CpuUsage,
                    ["MemoryJsHeapUsed"] = s.MemoryJsHeapUsed,
                    ["Fps"] = s.Fps
                });
            }
            return arr;
        }
        else if (providerId == "Network" && network != null)
        {
            var arr = new JsonArray();
            foreach (var r in network)
            {
                arr.Add(new JsonObject
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
            return arr;
        }
        return null;
    }
}
