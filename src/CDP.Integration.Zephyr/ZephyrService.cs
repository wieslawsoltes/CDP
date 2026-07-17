using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CDP.Integration.Core;

namespace CDP.Integration.Zephyr;

public class ZephyrService : ITestService
{
    public string ServiceName => "Zephyr";

    private HttpClient CreateClient(TestServiceConfig config)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Url.TrimEnd('/') + "/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<bool> ValidateConnectionAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        try
        {
            var response = await client.GetAsync("testcases", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ExportTestCaseAsync(TestServiceConfig config, TestCaseData testCase, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);

        var payload = new
        {
            projectKey = config.ProjectId,
            name = testCase.Title,
            objective = testCase.Description
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("testcases", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("key").GetString() ?? throw new InvalidOperationException("No testcase key returned.");
    }

    public async Task<string> PublishRunResultAsync(TestServiceConfig config, TestRunResultData runResult, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);

        // 1. Create or Find a Cycle (using MilestoneId or creating a new cycle)
        string cycleKey = config.MilestoneId;
        if (string.IsNullOrEmpty(cycleKey))
        {
            var cyclePayload = new
            {
                projectKey = config.ProjectId,
                name = runResult.TestName,
                folderId = string.IsNullOrEmpty(config.SuiteId) ? null : (int?)int.Parse(config.SuiteId)
            };
            var cycleContent = new StringContent(JsonSerializer.Serialize(cyclePayload), Encoding.UTF8, "application/json");
            var cycleResponse = await client.PostAsync("testcycles", cycleContent, cancellationToken);
            cycleResponse.EnsureSuccessStatusCode();

            var cycleJson = await cycleResponse.Content.ReadAsStringAsync(cancellationToken);
            using var cycleDoc = JsonDocument.Parse(cycleJson);
            cycleKey = cycleDoc.RootElement.GetProperty("key").GetString() ?? throw new InvalidOperationException("No cycle key returned.");
        }

        // 2. Submit test execution result with step results
        var resultPayload = new[]
        {
            new
            {
                testCaseKey = runResult.TestCaseId,
                statusName = runResult.Status.ToLowerInvariant() == "passed" ? "Pass" : "Fail",
                comment = runResult.Description,
                executionTime = (long)runResult.DurationMs,
                environmentName = string.IsNullOrEmpty(config.ConfigurationId) ? null : config.ConfigurationId,
                steps = runResult.Steps.Select(s => new
                {
                    statusName = s.Status.ToLowerInvariant() == "passed" ? "Pass" : "Fail",
                    comment = s.ErrorMessage ?? s.StepLog ?? "",
                    actualResult = s.Status == "Passed" ? "Successful execution" : s.ErrorMessage
                })
            }
        };
        var resultContent = new StringContent(JsonSerializer.Serialize(resultPayload), Encoding.UTF8, "application/json");
        var resultResponse = await client.PostAsync($"testruns/{cycleKey}/testresults", resultContent, cancellationToken);
        resultResponse.EnsureSuccessStatusCode();

        return cycleKey;
    }

    public async Task<List<MetadataItem>> GetMilestonesAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"testcycles?projectKey={config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        // Zephyr returns an array or cycle object wrapper. We assume cycle array.
        var values = doc.RootElement.GetProperty("values");
        foreach (var element in values.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("key").GetString() ?? "",
                Name = element.GetProperty("name").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetPlansAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"testplans?projectKey={config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        var values = doc.RootElement.GetProperty("values");
        foreach (var element in values.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("key").GetString() ?? "",
                Name = element.GetProperty("name").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetConfigurationsAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"environments?projectKey={config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        var values = doc.RootElement.GetProperty("values");
        foreach (var element in values.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("name").GetString() ?? "",
                Name = element.GetProperty("name").GetString() ?? ""
            });
        }
        return items;
    }
}
