using System;
using System.Collections.Generic;

namespace CDP.Integration.Core;

public static class IntegrationsFacade
{
    private static string _providerName = "";
    private static TestServiceConfig _config = new();

    public static void SetProvider(string providerName)
    {
        _providerName = providerName;
    }

    public static void Configure(string url, string username, string token, string projectId, string suiteId = "", string milestoneId = "", string planId = "", string configurationId = "")
    {
        _config = new TestServiceConfig
        {
            Url = url,
            Username = username,
            Token = token,
            ProjectId = projectId,
            SuiteId = suiteId,
            MilestoneId = milestoneId,
            PlanId = planId,
            ConfigurationId = configurationId
        };
    }

    public static bool VerifyConnection()
    {
        var service = TestServiceRegistry.Get(_providerName);
        if (service == null) return false;
        try
        {
            return service.ValidateConnectionAsync(_config).GetAwaiter().GetResult();
        }
        catch
        {
            return false;
        }
    }

    public static string ExportTestCase(string title, string description)
    {
        var service = TestServiceRegistry.Get(_providerName);
        if (service == null) return "Error: Service not found.";
        try
        {
            var testCase = new TestCaseData { Title = title, Description = description };
            return service.ExportTestCaseAsync(_config, testCase).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public static string PublishRunResult(string testCaseId, string testName, string status, double durationMs)
    {
        var service = TestServiceRegistry.Get(_providerName);
        if (service == null) return "Error: Service not found.";
        try
        {
            var runResult = new TestRunResultData
            {
                TestCaseId = testCaseId,
                TestName = testName,
                Status = status,
                DurationMs = durationMs,
                StartTime = DateTime.UtcNow.AddMilliseconds(-durationMs),
                EndTime = DateTime.UtcNow
            };
            return service.PublishRunResultAsync(_config, runResult).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
