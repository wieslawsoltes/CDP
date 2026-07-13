using System;
using System.Collections.Generic;

namespace CDP.Profiling.Analysis;

public class AnalyzedFlameBlock
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public int Depth { get; set; }
}

public class AnalyzedMethodStat
{
    public string MethodName { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public double SelfTimeMs { get; set; }
    public double SelfTimePct { get; set; }
    public double TotalTimeMs { get; set; }
    public double TotalTimePct { get; set; }
    public int HitCount { get; set; }
}

public class AnalyzedCallTreeNode
{
    public string Name { get; set; } = "";
    public double SelfTimeMs { get; set; }
    public double SelfTimePct { get; set; }
    public double TotalTimeMs { get; set; }
    public double TotalTimePct { get; set; }
    public int HitCount { get; set; }
    public List<AnalyzedCallTreeNode> Children { get; } = new();
}

public class AnalyzedMemoryStat
{
    public string TypeName { get; set; } = "";
    public long AllocatedBytes { get; set; }
    public double SizePct { get; set; }
    public int AllocationCount { get; set; }
    public double CountPct { get; set; }
}

public class AnalyzedCpuSession
{
    public string Name { get; set; } = "";
    public double TotalDurationMs { get; set; }
    public int TotalSamplesCount { get; set; }
    public List<AnalyzedFlameBlock> Blocks { get; } = new();
    public List<AnalyzedMethodStat> MethodStats { get; } = new();
    public List<AnalyzedCallTreeNode> CallTreeRoots { get; } = new();
}

public class AnalyzedMemorySession
{
    public string Name { get; set; } = "";
    public double TotalAllocatedBytes { get; set; }
    public int TotalAllocationsCount { get; set; }
    public List<AnalyzedMemoryStat> MemoryStats { get; } = new();
}
