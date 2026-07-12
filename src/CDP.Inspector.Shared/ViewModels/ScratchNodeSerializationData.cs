#nullable enable

using System;

namespace CdpInspectorApp.ViewModels;

public class ScratchPageNodeData
{
    public string? ScreenshotBase64 { get; set; }
    public bool IsSyncedWithTimeMachine { get; set; }
    public int PinnedFrameIndex { get; set; }

    public ScratchPageNodeData Clone()
    {
        return new ScratchPageNodeData
        {
            ScreenshotBase64 = this.ScreenshotBase64,
            IsSyncedWithTimeMachine = this.IsSyncedWithTimeMachine,
            PinnedFrameIndex = this.PinnedFrameIndex
        };
    }
}

public class ScratchImageDiffNodeData
{
    public string? LeftNodeId { get; set; }
    public string? RightNodeId { get; set; }
    public string LeftTitle { get; set; } = "";
    public string RightTitle { get; set; } = "";
    public double DiffPercentage { get; set; }

    public ScratchImageDiffNodeData Clone()
    {
        return new ScratchImageDiffNodeData
        {
            LeftNodeId = this.LeftNodeId,
            RightNodeId = this.RightNodeId,
            LeftTitle = this.LeftTitle,
            RightTitle = this.RightTitle,
            DiffPercentage = this.DiffPercentage
        };
    }
}

public class ScratchDomNodeData
{
    public string DomTreeJson { get; set; } = "";
    public int ElementCount { get; set; }
    public string SearchQuery { get; set; } = "";
    public string RawJsonData { get; set; } = "";
    public DateTime? Timestamp { get; set; }

    public ScratchDomNodeData Clone()
    {
        return new ScratchDomNodeData
        {
            DomTreeJson = this.DomTreeJson,
            ElementCount = this.ElementCount,
            SearchQuery = this.SearchQuery,
            RawJsonData = this.RawJsonData,
            Timestamp = this.Timestamp
        };
    }
}

public class ScratchPerformanceNodeData
{
    public string RawJsonData { get; set; } = "";
    public DateTime? Timestamp { get; set; }

    public ScratchPerformanceNodeData Clone()
    {
        return new ScratchPerformanceNodeData
        {
            RawJsonData = this.RawJsonData,
            Timestamp = this.Timestamp
        };
    }
}

public class ScratchApplicationNodeData
{
    public string RawJsonData { get; set; } = "";
    public DateTime? Timestamp { get; set; }

    public ScratchApplicationNodeData Clone()
    {
        return new ScratchApplicationNodeData
        {
            RawJsonData = this.RawJsonData,
            Timestamp = this.Timestamp
        };
    }
}

public class SemanticConsoleLog
{
    public string Timestamp { get; set; } = "";
    public string Level { get; set; } = "";
    public string Text { get; set; } = "";
}

public class SemanticNetworkRequest
{
    public string RequestId { get; set; } = "";
    public string Method { get; set; } = "";
    public string Url { get; set; } = "";
    public string Type { get; set; } = "";
    public int Status { get; set; }
    public double Time { get; set; }
}

public class SemanticPerformanceMetric
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
}
