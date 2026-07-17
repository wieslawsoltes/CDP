using System;
using System.Collections.Generic;
using CDP.Editor.Splits.Models;
using CdpInspectorApp.Controls;

namespace CdpGalleryApp.ViewModels;

public class ChartsPageViewModel : ViewModelBase
{
    private readonly double[] _timelineHistory;
    private SplitNode? _splitRoot;
    private BoxNode? _selectedPane;
    private IEnumerable<FlameBlock>? _flameBlocks;

    public IEnumerable<double> TimelineHistory => _timelineHistory;

    public SplitNode? SplitRoot
    {
        get => _splitRoot;
        set => RaiseAndSetIfChanged(ref _splitRoot, value);
    }

    public BoxNode? SelectedPane
    {
        get => _selectedPane;
        set => RaiseAndSetIfChanged(ref _selectedPane, value);
    }

    public IEnumerable<FlameBlock>? FlameBlocks
    {
        get => _flameBlocks;
        set => RaiseAndSetIfChanged(ref _flameBlocks, value);
    }

    public ChartsPageViewModel()
    {
        _timelineHistory = new double[]
        {
            12.0, 15.0, 14.0, 18.0, 25.0, 32.0, 28.0, 30.0, 45.0, 60.0,
            55.0, 40.0, 38.0, 35.0, 42.0, 50.0, 48.0, 44.0, 39.0, 35.0,
            30.0, 25.0, 22.0, 28.0, 34.0, 40.0, 48.0, 55.0, 62.0, 75.0,
            80.0, 72.0, 65.0, 58.0, 50.0, 45.0, 42.0, 38.0, 35.0, 30.0,
            28.0, 25.0, 27.0, 32.0, 38.0, 45.0, 50.0, 55.0, 60.0, 65.0
        };

        // Initialize Flame Chart mock data
        InitializeFlameBlocks();

        // Initialize Network Waterfall mock data
        InitializeNetworkRequests();

        // Initialize SuperSplit layout tree mapping each chart type to a BoxNode
        var panelCpu = new BoxNode("CPU", "CPU Profiling (Pie Chart)", "TimerIcon");
        var panelMem = new BoxNode("Memory", "Memory generations (Bar Chart)", "DeveloperBoardIcon");
        var panelTime = new BoxNode("Timeline", "History over time (Line Chart)", "HistoryIcon");
        var panelFlame = new BoxNode("Flame", "Call Tree Profile (Flame Chart)", "FlowchartIcon");
        var panelNet = new BoxNode("Network", "Network Waterfall (Waterfall Chart)", "GlobeIcon");

        var leftSplit = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, panelCpu, panelMem)
        {
            SplitterRatio = 0.5
        };

        var rightRightSplit = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, panelFlame, panelNet)
        {
            SplitterRatio = 0.5
        };

        var rightSplit = new SplitContainerNode(Avalonia.Layout.Orientation.Vertical, panelTime, rightRightSplit)
        {
            SplitterRatio = 0.33
        };

        _splitRoot = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, leftSplit, rightSplit)
        {
            SplitterRatio = 0.5
        };

        _selectedPane = panelCpu;
    }

    private void InitializeFlameBlocks()
    {
        _flameBlocks = new List<FlameBlock>
        {
            // Depth 0
            new FlameBlock { Name = "Main", Url = "program.cs:10", StartTimeMs = 0, EndTimeMs = 100, Depth = 0, SelfTimeMs = 0, SelfTimePct = 0, TotalTimeMs = 100, TotalTimePct = 100 },
            
            // Depth 1
            new FlameBlock { Name = "Run", Url = "runner.cs:22", StartTimeMs = 0, EndTimeMs = 60, Depth = 1, SelfTimeMs = 0, SelfTimePct = 0, TotalTimeMs = 60, TotalTimePct = 60 },
            new FlameBlock { Name = "Cleanup", Url = "cleanup.cs:12", StartTimeMs = 60, EndTimeMs = 100, Depth = 1, SelfTimeMs = 25, SelfTimePct = 62.5, TotalTimeMs = 40, TotalTimePct = 40 },
            
            // Depth 2
            new FlameBlock { Name = "Parse", Url = "parser.cs:55", StartTimeMs = 0, EndTimeMs = 30, Depth = 2, SelfTimeMs = 0, SelfTimePct = 0, TotalTimeMs = 30, TotalTimePct = 30 },
            new FlameBlock { Name = "Execute", Url = "executor.cs:8", StartTimeMs = 30, EndTimeMs = 60, Depth = 2, SelfTimeMs = 15, SelfTimePct = 50, TotalTimeMs = 30, TotalTimePct = 30 },
            new FlameBlock { Name = "Dispose", Url = "dispose.cs:5", StartTimeMs = 65, EndTimeMs = 80, Depth = 2, SelfTimeMs = 15, SelfTimePct = 100, TotalTimeMs = 15, TotalTimePct = 15 },
            
            // Depth 3
            new FlameBlock { Name = "Tokenize", Url = "lexer.cs:40", StartTimeMs = 0, EndTimeMs = 15, Depth = 3, SelfTimeMs = 15, SelfTimePct = 100, TotalTimeMs = 15, TotalTimePct = 15 },
            new FlameBlock { Name = "BuildAST", Url = "ast.cs:105", StartTimeMs = 15, EndTimeMs = 30, Depth = 3, SelfTimeMs = 15, SelfTimePct = 100, TotalTimeMs = 15, TotalTimePct = 15 },
            new FlameBlock { Name = "ResolveReferences", Url = "binder.cs:64", StartTimeMs = 35, EndTimeMs = 50, Depth = 3, SelfTimeMs = 15, SelfTimePct = 100, TotalTimeMs = 15, TotalTimePct = 15 },
            new FlameBlock { Name = "WriteOutput", Url = "writer.cs:98", StartTimeMs = 50, EndTimeMs = 60, Depth = 3, SelfTimeMs = 10, SelfTimePct = 100, TotalTimeMs = 10, TotalTimePct = 10 }
        };
    }

    private double _cpuScripting = 30.0;
    private double _cpuRendering = 20.0;
    private double _cpuLayout = 15.0;
    private double _cpuSystem = 10.0;
    private double _cpuIdle = 25.0;

    private long _gen0Size = 50 * 1024 * 1024; // 50 MB
    private long _gen1Size = 30 * 1024 * 1024; // 30 MB
    private long _gen2Size = 100 * 1024 * 1024; // 100 MB
    private long _lohSize = 40 * 1024 * 1024; // 40 MB

    public double CpuScripting
    {
        get => _cpuScripting;
        set => RaiseAndSetIfChanged(ref _cpuScripting, value);
    }

    public double CpuRendering
    {
        get => _cpuRendering;
        set => RaiseAndSetIfChanged(ref _cpuRendering, value);
    }

    public double CpuLayout
    {
        get => _cpuLayout;
        set => RaiseAndSetIfChanged(ref _cpuLayout, value);
    }

    public double CpuSystem
    {
        get => _cpuSystem;
        set => RaiseAndSetIfChanged(ref _cpuSystem, value);
    }

    public double CpuIdle
    {
        get => _cpuIdle;
        set => RaiseAndSetIfChanged(ref _cpuIdle, value);
    }

    public long Gen0Size
    {
        get => _gen0Size;
        set
        {
            if (RaiseAndSetIfChanged(ref _gen0Size, value))
            {
                OnPropertyChanged(nameof(Gen0SizeMb));
            }
        }
    }

    public long Gen1Size
    {
        get => _gen1Size;
        set
        {
            if (RaiseAndSetIfChanged(ref _gen1Size, value))
            {
                OnPropertyChanged(nameof(Gen1SizeMb));
            }
        }
    }

    public long Gen2Size
    {
        get => _gen2Size;
        set
        {
            if (RaiseAndSetIfChanged(ref _gen2Size, value))
            {
                OnPropertyChanged(nameof(Gen2SizeMb));
            }
        }
    }

    public long LohSize
    {
        get => _lohSize;
        set
        {
            if (RaiseAndSetIfChanged(ref _lohSize, value))
            {
                OnPropertyChanged(nameof(LohSizeMb));
            }
        }
    }

    // Helper properties for sliders (bound in Megabytes)
    public double Gen0SizeMb
    {
        get => _gen0Size / (1024.0 * 1024.0);
        set => Gen0Size = (long)(value * 1024.0 * 1024.0);
    }

    public double Gen1SizeMb
    {
        get => _gen1Size / (1024.0 * 1024.0);
        set => Gen1Size = (long)(value * 1024.0 * 1024.0);
    }

    public double Gen2SizeMb
    {
        get => _gen2Size / (1024.0 * 1024.0);
        set => Gen2Size = (long)(value * 1024.0 * 1024.0);
    }

    public double LohSizeMb
    {
        get => _lohSize / (1024.0 * 1024.0);
        set => LohSize = (long)(value * 1024.0 * 1024.0);
    }
    public class MockRequestModel
    {
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public double StartOffsetPercent { get; set; }
        public double TtfbPercent { get; set; }
        public double DownloadPercent { get; set; }
    }

    private IEnumerable<MockRequestModel>? _networkRequests;

    public IEnumerable<MockRequestModel>? NetworkRequests
    {
        get => _networkRequests;
        set => RaiseAndSetIfChanged(ref _networkRequests, value);
    }

    private void InitializeNetworkRequests()
    {
        _networkRequests = new List<MockRequestModel>
        {
            new MockRequestModel { Url = "index.html", Method = "GET", Status = "200", Type = "document", Time = "120 ms", StartOffsetPercent = 0.0, TtfbPercent = 0.05, DownloadPercent = 0.05 },
            new MockRequestModel { Url = "styles.css", Method = "GET", Status = "200", Type = "stylesheet", Time = "85 ms", StartOffsetPercent = 0.10, TtfbPercent = 0.03, DownloadPercent = 0.04 },
            new MockRequestModel { Url = "bundle.js", Method = "GET", Status = "200", Type = "script", Time = "240 ms", StartOffsetPercent = 0.12, TtfbPercent = 0.07, DownloadPercent = 0.13 },
            new MockRequestModel { Url = "logo.svg", Method = "GET", Status = "200", Type = "image", Time = "95 ms", StartOffsetPercent = 0.15, TtfbPercent = 0.04, DownloadPercent = 0.04 },
            new MockRequestModel { Url = "api/data", Method = "POST", Status = "201", Type = "fetch", Time = "310 ms", StartOffsetPercent = 0.35, TtfbPercent = 0.18, DownloadPercent = 0.08 },
            new MockRequestModel { Url = "analytics.js", Method = "GET", Status = "304", Type = "script", Time = "45 ms", StartOffsetPercent = 0.50, TtfbPercent = 0.03, DownloadPercent = 0.01 }
        };
    }
}
