using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Controls.Templates;
using CDP.Editor.Splits.Controls;
using CdpGalleryApp.ViewModels;
using CdpInspectorApp.Controls;

namespace CdpGalleryApp.Views;

public partial class ChartsPage : UserControl
{
    public ChartsPage()
    {
        InitializeComponent();

        var splitControl = this.FindControl<SuperSplit>("SplitControl");
        if (splitControl != null)
        {
            splitControl.ViewResolver = ResolveChartView;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private Control ResolveChartView(string viewName, SuperSplitBox? splitBox)
    {
        return viewName switch
        {
            "CPU" => CreateCpuView(),
            "Memory" => CreateMemoryView(),
            "Timeline" => CreateTimelineView(),
            "Flame" => CreateFlameView(),
            "Network" => CreateNetworkView(),
            _ => new Border
            {
                Background = Brush.Parse("#1e1e1e"),
                Padding = new Thickness(16),
                Child = new TextBlock { Text = $"Unknown view: {viewName}", Foreground = Brushes.Red }
            }
        };
    }

    private Border WrapInCard(string title, Control content, Control? controls = null)
    {
        var border = new Border
        {
            Background = Brush.Parse("#1c1c1c"),
            BorderBrush = Brush.Parse("#2d2d2d"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16),
            Margin = new Thickness(4)
        };

        var mainGrid = new Grid { RowDefinitions = new RowDefinitions("Auto, *") };
        
        var header = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Foreground = Brush.Parse("#8ab4f8"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        mainGrid.Children.Add(header);
        Grid.SetRow(header, 0);

        Control body;
        if (controls != null)
        {
            var splitGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*, 200"), Margin = new Thickness(0) };
            
            var chartWrap = new Border
            {
                Background = Brush.Parse("#121212"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Height = 220,
                Child = content
            };
            splitGrid.Children.Add(chartWrap);
            Grid.SetColumn(chartWrap, 0);

            var controlWrap = new Border
            {
                Margin = new Thickness(16, 0, 0, 0),
                Child = controls
            };
            splitGrid.Children.Add(controlWrap);
            Grid.SetColumn(controlWrap, 1);

            body = splitGrid;
        }
        else
        {
            var chartWrap = new Border
            {
                Background = Brush.Parse("#121212"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Child = content
            };
            body = chartWrap;
        }

        mainGrid.Children.Add(body);
        Grid.SetRow(body, 1);

        border.Child = mainGrid;
        return border;
    }

    private Control CreateCpuView()
    {
        var chart = new CpuPieChart();
        chart.Bind(CpuPieChart.CpuScriptingProperty, new Binding("CpuScripting"));
        chart.Bind(CpuPieChart.CpuRenderingProperty, new Binding("CpuRendering"));
        chart.Bind(CpuPieChart.CpuLayoutProperty, new Binding("CpuLayout"));
        chart.Bind(CpuPieChart.CpuSystemProperty, new Binding("CpuSystem"));
        chart.Bind(CpuPieChart.CpuIdleProperty, new Binding("CpuIdle"));

        var controls = new StackPanel { Spacing = 10 };
        controls.Children.Add(CreateSlider("Scripting: {0:F0}%", "CpuScripting", 0, 100));
        controls.Children.Add(CreateSlider("Rendering: {0:F0}%", "CpuRendering", 0, 100));
        controls.Children.Add(CreateSlider("Layout: {0:F0}%", "CpuLayout", 0, 100));
        controls.Children.Add(CreateSlider("System: {0:F0}%", "CpuSystem", 0, 100));
        controls.Children.Add(CreateSlider("Idle: {0:F0}%", "CpuIdle", 0, 100));

        return WrapInCard("CPU Profiling Metrics (Pie Chart)", chart, controls);
    }

    private Control CreateMemoryView()
    {
        var chart = new GenerationsBarChart { Height = 40, VerticalAlignment = VerticalAlignment.Center };
        chart.Bind(GenerationsBarChart.Gen0SizeProperty, new Binding("Gen0Size"));
        chart.Bind(GenerationsBarChart.Gen1SizeProperty, new Binding("Gen1Size"));
        chart.Bind(GenerationsBarChart.Gen2SizeProperty, new Binding("Gen2Size"));
        chart.Bind(GenerationsBarChart.LohSizeProperty, new Binding("LohSize"));

        var controls = new StackPanel { Spacing = 10 };
        controls.Children.Add(CreateSlider("Gen 0: {0:F1} MB", "Gen0SizeMb", 0.1, 200));
        controls.Children.Add(CreateSlider("Gen 1: {0:F1} MB", "Gen1SizeMb", 0.1, 200));
        controls.Children.Add(CreateSlider("Gen 2: {0:F1} MB", "Gen2SizeMb", 0.1, 500));
        controls.Children.Add(CreateSlider("LOH Size: {0:F1} MB", "LohSizeMb", 0.1, 500));

        return WrapInCard("GC Memory Allocations (Bar Chart)", chart, controls);
    }

    private Control CreateTimelineView()
    {
        var chart = new TimelineChart
        {
            FillColor = Color.Parse("#208ab4f8"),
            StrokeColor = Color.Parse("#8ab4f8"),
            Unit = "ms",
            MaxDisplayValue = 100,
            Height = 180
        };
        chart.Bind(TimelineChart.HistoryProperty, new Binding("TimelineHistory"));

        return WrapInCard("Performance Metric History over Time (Timeline Chart)", chart);
    }

    private Control CreateFlameView()
    {
        var chart = new FlameChart
        {
            ZoomScale = 1.0,
            OffsetX = 0.0,
            OffsetY = 0.0,
            Height = 220
        };
        chart.Bind(FlameChart.BlocksProperty, new Binding("FlameBlocks"));

        return WrapInCard("Call Tree Profile (Flame Chart)", chart);
    }

    private Control CreateNetworkView()
    {
        var mainDock = new DockPanel();

        // Create Header Row
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, 80, 80, 80, 80, 150"),
            Height = 24,
            Background = Brush.Parse("#1a1a1a")
        };
        headerGrid.Children.Add(CreateHeaderCell("Name / URL", 0));
        headerGrid.Children.Add(CreateHeaderCell("Method", 1));
        headerGrid.Children.Add(CreateHeaderCell("Status", 2));
        headerGrid.Children.Add(CreateHeaderCell("Type", 3));
        headerGrid.Children.Add(CreateHeaderCell("Time", 4));
        headerGrid.Children.Add(CreateHeaderCell("Waterfall", 5));
        
        DockPanel.SetDock(headerGrid, Dock.Top);
        mainDock.Children.Add(headerGrid);

        // Scrollable ItemsControl
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var items = new ItemsControl();
        items.Bind(ItemsControl.ItemsSourceProperty, new Binding("NetworkRequests"));

        items.ItemTemplate = new FuncDataTemplate<ChartsPageViewModel.MockRequestModel>((data, namescope) =>
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, 80, 80, 80, 80, 150"),
                Height = 28
            };

            // Border bottom line
            var border = new Border
            {
                BorderBrush = Brush.Parse("#2d2d2d"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 28
            };

            var urlTb = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush.Parse("#e8eaed"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            urlTb.Bind(TextBlock.TextProperty, new Binding("Url"));
            grid.Children.Add(urlTb);
            Grid.SetColumn(urlTb, 0);

            var methodTb = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush.Parse("#fdd663"),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            methodTb.Bind(TextBlock.TextProperty, new Binding("Method"));
            grid.Children.Add(methodTb);
            Grid.SetColumn(methodTb, 1);

            var statusTb = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush.Parse("#81c995"),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusTb.Bind(TextBlock.TextProperty, new Binding("Status"));
            grid.Children.Add(statusTb);
            Grid.SetColumn(statusTb, 2);

            var typeTb = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush.Parse("#9aa0a6"),
                VerticalAlignment = VerticalAlignment.Center
            };
            typeTb.Bind(TextBlock.TextProperty, new Binding("Type"));
            grid.Children.Add(typeTb);
            Grid.SetColumn(typeTb, 3);

            var timeTb = new TextBlock
            {
                FontSize = 11,
                Foreground = Brush.Parse("#8ab4f8"),
                VerticalAlignment = VerticalAlignment.Center
            };
            timeTb.Bind(TextBlock.TextProperty, new Binding("Time"));
            grid.Children.Add(timeTb);
            Grid.SetColumn(timeTb, 4);

            var waterfallContainer = new Grid
            {
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var waterfallBar = new WaterfallBar
            {
                Height = 6,
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            waterfallBar.Bind(WaterfallBar.StartOffsetPercentProperty, new Binding("StartOffsetPercent"));
            waterfallBar.Bind(WaterfallBar.TtfbPercentProperty, new Binding("TtfbPercent"));
            waterfallBar.Bind(WaterfallBar.DownloadPercentProperty, new Binding("DownloadPercent"));

            waterfallContainer.Children.Add(waterfallBar);
            grid.Children.Add(waterfallContainer);
            Grid.SetColumn(waterfallContainer, 5);

            border.Child = grid;
            return border;
        });

        scroll.Content = items;
        mainDock.Children.Add(scroll);

        return WrapInCard("Network Waterfall (Waterfall Chart)", mainDock);
    }

    private Control CreateHeaderCell(string text, int column)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#9aa0a6"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(column == 0 ? 6 : 0, 0, 0, 0)
        };
        Grid.SetColumn(tb, column);
        return tb;
    }

    private StackPanel CreateSlider(string labelFormat, string valuePath, double min, double max)
    {
        var stack = new StackPanel { Spacing = 4 };
        var tb = new TextBlock { FontSize = 11, Foreground = Brush.Parse("#e8eaed") };
        tb.Bind(TextBlock.TextProperty, new Binding(valuePath) { StringFormat = labelFormat });
        
        var slider = new Slider { Minimum = min, Maximum = max };
        slider.Bind(Slider.ValueProperty, new Binding(valuePath) { Mode = BindingMode.TwoWay });
        
        stack.Children.Add(tb);
        stack.Children.Add(slider);
        return stack;
    }
}
