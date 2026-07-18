using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CDP.Integration.Core;

public class TestServiceConfig
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string SuiteId { get; set; } = "";
    public string MilestoneId { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string ConfigurationId { get; set; } = "";
    public string AssigneeId { get; set; } = "";
    public Dictionary<string, string> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class TestCaseData
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<TestStepData> Steps { get; set; } = new();
}

public class TestStepData
{
    public string Action { get; set; } = "";
    public string? Selector { get; set; }
    public string? Value { get; set; }
}

public class TestStepResultData
{
    public int Index { get; set; }
    public string Action { get; set; } = "";
    public string Status { get; set; } = ""; // Passed, Failed
    public string? ErrorMessage { get; set; }
    public double DurationMs { get; set; }
    public string? ScreenshotBase64 { get; set; }
    public string? StepLog { get; set; }
}

public class TestRunResultData
{
    public string TestCaseId { get; set; } = "";
    public string TestName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = ""; // Passed, Failed
    public double DurationMs { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<TestStepResultData> Steps { get; set; } = new();
    public string? HtmlReportPath { get; set; }
    public string? PdfReportPath { get; set; }
    public List<string> VideoFramePaths { get; set; } = new();
}

public class MetadataItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public interface ITestService
{
    string ServiceName { get; }
    Task<bool> ValidateConnectionAsync(TestServiceConfig config, CancellationToken cancellationToken = default);
    Task<string> ExportTestCaseAsync(TestServiceConfig config, TestCaseData testCase, CancellationToken cancellationToken = default);
    Task<string> PublishRunResultAsync(TestServiceConfig config, TestRunResultData runResult, CancellationToken cancellationToken = default);
    Task<List<MetadataItem>> GetMilestonesAsync(TestServiceConfig config, CancellationToken cancellationToken = default);
    Task<List<MetadataItem>> GetPlansAsync(TestServiceConfig config, CancellationToken cancellationToken = default);
    Task<List<MetadataItem>> GetConfigurationsAsync(TestServiceConfig config, CancellationToken cancellationToken = default);
}

public static class TestServiceRegistry
{
    private static readonly Dictionary<string, ITestService> _services = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(ITestService service)
    {
        _services[service.ServiceName] = service;
    }

    public static ITestService? Get(string serviceName)
    {
        return _services.TryGetValue(serviceName, out var service) ? service : null;
    }

    public static IEnumerable<ITestService> GetAll()
    {
        return _services.Values;
    }
}
