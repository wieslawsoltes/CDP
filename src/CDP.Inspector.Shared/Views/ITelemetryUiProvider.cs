#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.Views;

public interface ITelemetryUiProvider
{
    string ProviderId { get; }
    string Name { get; }
    Control CreateView(JsonNode? stepData, JsonNode? runData);
    void UpdateView(Control view, JsonNode? stepData, JsonNode? runData);
}

public static class TelemetryUiRegistry
{
    private static readonly List<ITelemetryUiProvider> _uiProviders = new();

    public static IReadOnlyList<ITelemetryUiProvider> UiProviders => _uiProviders;

    public static void Register(ITelemetryUiProvider uiProvider)
    {
        if (uiProvider == null) return;
        if (!_uiProviders.Exists(p => p.ProviderId == uiProvider.ProviderId))
        {
            _uiProviders.Add(uiProvider);
        }
    }

    static TelemetryUiRegistry()
    {
        Register(new PerformanceTelemetryUiProvider());
        Register(new NetworkTelemetryUiProvider());
    }
}

public class PerformanceTelemetryUiProvider : ITelemetryUiProvider
{
    public string ProviderId => "Performance";
    public string Name => "Performance";

    public Control CreateView(JsonNode? stepData, JsonNode? runData)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *"),
            Margin = new Thickness(6)
        };

        var statsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, Auto, 15, Auto, Auto, 15, Auto, Auto"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var cpuLabel = new TextBlock { Text = "CPU Usage: ", FontSize = 9, Foreground = Brush.Parse("#8a8f98") };
        var cpuText = new TextBlock { Name = "lblStepCpu", FontSize = 9, Foreground = Brush.Parse("#e8eaed"), FontWeight = FontWeight.SemiBold };

        var heapLabel = new TextBlock { Text = "JS Heap: ", FontSize = 9, Foreground = Brush.Parse("#8a8f98") };
        var heapText = new TextBlock { Name = "lblStepMemory", FontSize = 9, Foreground = Brush.Parse("#e8eaed"), FontWeight = FontWeight.SemiBold };

        var fpsLabel = new TextBlock { Text = "FPS / DOM: ", FontSize = 9, Foreground = Brush.Parse("#8a8f98") };
        var fpsText = new TextBlock { Name = "lblStepFpsDom", FontSize = 9, Foreground = Brush.Parse("#e8eaed"), FontWeight = FontWeight.SemiBold };

        Grid.SetColumn(cpuLabel, 0);
        Grid.SetColumn(cpuText, 1);
        Grid.SetColumn(heapLabel, 3);
        Grid.SetColumn(heapText, 4);
        Grid.SetColumn(fpsLabel, 6);
        Grid.SetColumn(fpsText, 7);

        statsGrid.Children.Add(cpuLabel);
        statsGrid.Children.Add(cpuText);
        statsGrid.Children.Add(heapLabel);
        statsGrid.Children.Add(heapText);
        statsGrid.Children.Add(fpsLabel);
        statsGrid.Children.Add(fpsText);

        grid.Children.Add(statsGrid);
        Grid.SetRow(statsGrid, 0);

        var border = new Border
        {
            BorderBrush = Brush.Parse("#2d313f"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#0f1015"),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var imgChart = new Image
        {
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        border.Child = imgChart;
        grid.Children.Add(border);
        Grid.SetRow(border, 1);

        UpdateDetails(cpuText, heapText, fpsText, imgChart, stepData, runData);

        imgChart.SizeChanged += (s, e) => {
            UpdateDetails(cpuText, heapText, fpsText, imgChart, stepData, runData);
        };

        return grid;
    }

    public void UpdateView(Control view, JsonNode? stepData, JsonNode? runData)
    {
        if (view is Grid grid && grid.Children.Count >= 2)
        {
            var statsGrid = grid.Children[0] as Grid;
            var border = grid.Children[1] as Border;
            var imgChart = border?.Child as Image;

            if (statsGrid != null && imgChart != null)
            {
                var cpuText = statsGrid.Children.OfType<TextBlock>().FirstOrDefault(c => c.Name == "lblStepCpu");
                var heapText = statsGrid.Children.OfType<TextBlock>().FirstOrDefault(c => c.Name == "lblStepMemory");
                var fpsText = statsGrid.Children.OfType<TextBlock>().FirstOrDefault(c => c.Name == "lblStepFpsDom");

                if (cpuText != null && heapText != null && fpsText != null)
                {
                    UpdateDetails(cpuText, heapText, fpsText, imgChart, stepData, runData);
                }
            }
        }
    }

    private void UpdateDetails(TextBlock cpuText, TextBlock heapText, TextBlock fpsText, Image imgChart, JsonNode? stepData, JsonNode? runData)
    {
        if (stepData == null)
        {
            cpuText.Text = "0.0 %";
            heapText.Text = "0.00 / 0.00 MB";
            fpsText.Text = "60.0 FPS / 0 nodes";
            imgChart.Source = null;
            return;
        }

        double cpu = stepData["CpuUsage"]?.GetValue<double>() ?? 0;
        double jsUsed = stepData["MemoryJsHeapUsed"]?.GetValue<double>() ?? 0;
        double jsTotal = stepData["MemoryJsHeapTotal"]?.GetValue<double>() ?? 0;
        double fps = stepData["Fps"]?.GetValue<double>() ?? 0;
        int domNodes = stepData["DomNodes"]?.GetValue<int>() ?? 0;

        cpuText.Text = $"{cpu:F1} %";
        heapText.Text = $"{jsUsed:F2} / {jsTotal:F2} MB";
        fpsText.Text = $"{fps:F1} FPS / {domNodes} nodes";

        if (runData is JsonArray timeline && timeline.Count > 0)
        {
            var list = new List<RunMetricSample>();
            foreach (var n in timeline)
            {
                if (n == null) continue;
                list.Add(new RunMetricSample
                {
                    RelativeTimeMs = n["RelativeTimeMs"]?.GetValue<double>() ?? 0,
                    CpuUsage = n["CpuUsage"]?.GetValue<double>() ?? 0,
                    MemoryJsHeapUsed = n["MemoryJsHeapUsed"]?.GetValue<double>() ?? 0,
                    Fps = n["Fps"]?.GetValue<double>() ?? 0
                });
            }

            double relativeStartMs = stepData["RelativeStartMs"]?.GetValue<double>() ?? 0;
            double durationMs = stepData["DurationMs"]?.GetValue<double>() ?? 0;

            int w = (int)imgChart.Bounds.Width;
            int h = (int)imgChart.Bounds.Height;
            if (w <= 0) w = 800;
            if (h <= 0) h = 120;

            var chartService = new TelemetryChartService();
            imgChart.Source = chartService.RenderPerformanceChart(list, relativeStartMs, durationMs, w, h);
        }
        else
        {
            imgChart.Source = null;
        }
    }
}

public class NetworkTelemetryUiProvider : ITelemetryUiProvider
{
    public string ProviderId => "Network";
    public string Name => "Network Waterfall";

    public Control CreateView(JsonNode? stepData, JsonNode? runData)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *"),
            Margin = new Thickness(6)
        };

        var summaryGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, Auto"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var netLabel = new TextBlock { Text = "Network Traffic: ", FontSize = 9, Foreground = Brush.Parse("#8a8f98") };
        var summaryText = new TextBlock { Name = "lblStepNetwork", FontSize = 9, Foreground = Brush.Parse("#e8eaed"), FontWeight = FontWeight.SemiBold };

        Grid.SetColumn(netLabel, 0);
        Grid.SetColumn(summaryText, 1);

        summaryGrid.Children.Add(netLabel);
        summaryGrid.Children.Add(summaryText);

        grid.Children.Add(summaryGrid);
        Grid.SetRow(summaryGrid, 0);

        var listNetwork = new ListBox
        {
            Background = Brush.Parse("#0f1015"),
            BorderBrush = Brush.Parse("#2d313f"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        listNetwork.ItemTemplate = new FuncDataTemplate<PlaybackNetworkItemViewModel>((item, namescope) =>
        {
            if (item == null) return new Panel();

            var itemGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("35, *, 30, 45, 110"),
                Margin = new Thickness(0, 1)
            };

            var txtMethod = new TextBlock { Text = item.Method, Foreground = Brush.Parse("#3b82f6"), FontWeight = FontWeight.Bold, FontSize = 9, VerticalAlignment = VerticalAlignment.Center };
            var txtUrl = new TextBlock { Text = item.DisplayUrl, Foreground = Brush.Parse("#e8eaed"), FontSize = 9, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0) };
            var txtStatus = new TextBlock { Text = item.Status, Foreground = Brush.Parse(item.StatusColor), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var txtDur = new TextBlock { Text = item.DurationText, Foreground = Brush.Parse("#8a8f98"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            var borderOuter = new Border
            {
                Background = Brush.Parse("#1c1e26"),
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(8, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };

            var borderInner = new Border
            {
                Background = Brush.Parse("#2563eb"),
                CornerRadius = new CornerRadius(2),
                Height = 8,
                Width = item.TimelineWidth,
                Margin = item.TimelineMargin,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            borderOuter.Child = borderInner;

            Grid.SetColumn(txtMethod, 0);
            Grid.SetColumn(txtUrl, 1);
            Grid.SetColumn(txtStatus, 2);
            Grid.SetColumn(txtDur, 3);
            Grid.SetColumn(borderOuter, 4);

            itemGrid.Children.Add(txtMethod);
            itemGrid.Children.Add(txtUrl);
            itemGrid.Children.Add(txtStatus);
            itemGrid.Children.Add(txtDur);
            itemGrid.Children.Add(borderOuter);

            return itemGrid;
        });

        grid.Children.Add(listNetwork);
        Grid.SetRow(listNetwork, 1);

        UpdateDetails(summaryText, listNetwork, stepData, runData);

        return grid;
    }

    public void UpdateView(Control view, JsonNode? stepData, JsonNode? runData)
    {
        if (view is Grid grid && grid.Children.Count >= 2)
        {
            var summaryGrid = grid.Children[0] as Grid;
            var listNetwork = grid.Children[1] as ListBox;

            if (summaryGrid != null && listNetwork != null)
            {
                var summaryText = summaryGrid.Children.OfType<TextBlock>().FirstOrDefault(c => c.Name == "lblStepNetwork");
                if (summaryText != null)
                {
                    UpdateDetails(summaryText, listNetwork, stepData, runData);
                }
            }
        }
    }

    private void UpdateDetails(TextBlock summaryText, ListBox listNetwork, JsonNode? stepData, JsonNode? runData)
    {
        if (stepData == null)
        {
            summaryText.Text = "0 requests / 0.00 KB";
            listNetwork.ItemsSource = null;
            return;
        }

        int count = stepData["NetworkRequestCount"]?.GetValue<int>() ?? 0;
        long bytes = stepData["NetworkResponseBytes"]?.GetValue<long>() ?? 0;

        summaryText.Text = $"{count} requests / {(bytes / 1024.0):F2} KB";

        if (runData is JsonArray timeline && timeline.Count > 0)
        {
            var stepStartMs = stepData["RelativeStartMs"]?.GetValue<double>() ?? 0;
            var durationMs = stepData["DurationMs"]?.GetValue<double>() ?? 0;

            var list = new List<NetworkReportItem>();
            foreach (var n in timeline)
            {
                if (n == null) continue;
                list.Add(new NetworkReportItem
                {
                    RequestId = n["RequestId"]?.GetValue<string>() ?? "",
                    Url = n["Url"]?.GetValue<string>() ?? "",
                    Method = n["Method"]?.GetValue<string>() ?? "",
                    Status = n["Status"]?.GetValue<string>() ?? "",
                    RelativeStartMs = n["RelativeStartMs"]?.GetValue<double>() ?? 0,
                    DurationMs = n["DurationMs"]?.GetValue<double>() ?? 0,
                    EncodedDataLength = n["EncodedDataLength"]?.GetValue<long>() ?? 0
                });
            }

            var stepRequests = list
                .Where(r => r.RelativeStartMs >= stepStartMs && r.RelativeStartMs <= (stepStartMs + durationMs))
                .Select(r => new PlaybackNetworkItemViewModel(r, stepStartMs, durationMs))
                .ToList();

            listNetwork.ItemsSource = stepRequests;
        }
        else
        {
            listNetwork.ItemsSource = null;
        }
    }
}
