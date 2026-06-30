#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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

        var imgChart = this.FindControl<Image>("imgStepChart");
        if (imgChart != null)
        {
            imgChart.SizeChanged += (s, e) => UpdateDetails();
        }
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
        var chartService = ChartService ?? new TelemetryChartService();

        var lblCpu = this.FindControl<TextBlock>("lblStepCpu");
        var lblMemory = this.FindControl<TextBlock>("lblStepMemory");
        var lblFpsDom = this.FindControl<TextBlock>("lblStepFpsDom");
        var lblNetwork = this.FindControl<TextBlock>("lblStepNetwork");
        var listNetwork = this.FindControl<ListBox>("listStepNetwork");
        var imgChart = this.FindControl<Image>("imgStepChart");

        if (step == null)
        {
            if (lblCpu != null) lblCpu.Text = "0.0 %";
            if (lblMemory != null) lblMemory.Text = "0.0 / 0.0 MB";
            if (lblFpsDom != null) lblFpsDom.Text = "60.0 FPS / 0 nodes";
            if (lblNetwork != null) lblNetwork.Text = "0 requests / 0.00 KB";
            if (listNetwork != null) listNetwork.ItemsSource = null;
            if (imgChart != null) imgChart.Source = null;
            return;
        }

        if (lblCpu != null) lblCpu.Text = $"{step.CpuUsage:F1} %";
        if (lblMemory != null) lblMemory.Text = $"{step.MemoryJsHeapUsed:F2} / {step.MemoryJsHeapTotal:F2} MB";
        if (lblFpsDom != null) lblFpsDom.Text = $"{step.Fps:F1} FPS / {step.DomNodes} nodes";
        if (lblNetwork != null) lblNetwork.Text = $"{step.NetworkRequestCount} requests / {(step.NetworkResponseBytes / 1024.0):F2} KB";

        if (listNetwork != null)
        {
            var stepRequests = network?
                .Where(r => r.RelativeStartMs >= step.RelativeStartMs && r.RelativeStartMs <= (step.RelativeStartMs + step.DurationMs))
                .Select(r => new PlaybackNetworkItemViewModel(r, step.RelativeStartMs, step.DurationMs))
                .ToList() ?? new List<PlaybackNetworkItemViewModel>();

            listNetwork.ItemsSource = stepRequests;
        }

        if (imgChart != null && metrics != null)
        {
            int w = (int)imgChart.Bounds.Width;
            int h = (int)imgChart.Bounds.Height;
            if (w <= 0) w = 800;
            if (h <= 0) h = 150;

            imgChart.Source = chartService.RenderPerformanceChart(metrics, step.RelativeStartMs, step.DurationMs, w, h);
        }
    }
}
