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

namespace CDP.Integration.Xray;

public class XrayService : ITestService
{
    public string ServiceName => "Xray";

    private HttpClient CreateClient(TestServiceConfig config, string? token = null)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Url.TrimEnd('/') + "/api/v2/");
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task<string> AuthenticateAsync(HttpClient client, TestServiceConfig config, CancellationToken cancellationToken)
    {
        var payload = new
        {
            client_id = config.Username,
            client_secret = config.Token
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("authenticate", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadAsStringAsync(cancellationToken);
        return token.Trim('"');
    }

    public async Task<bool> ValidateConnectionAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        try
        {
            var token = await AuthenticateAsync(client, config, cancellationToken);
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ExportTestCaseAsync(TestServiceConfig config, TestCaseData testCase, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var token = await AuthenticateAsync(client, config, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            projectKey = config.ProjectId,
            testKey = "",
            info = new
            {
                summary = testCase.Title,
                description = testCase.Description,
                type = "Manual"
            },
            steps = testCase.Steps.Select(s => new
            {
                action = s.Action,
                data = s.Selector ?? "",
                result = s.Value ?? ""
            })
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("import/test", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("No test ID returned.");
    }

    public async Task<string> PublishRunResultAsync(TestServiceConfig config, TestRunResultData runResult, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var token = await AuthenticateAsync(client, config, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Xray JSON format import execution
        var payload = new
        {
            info = new
            {
                summary = runResult.TestName,
                description = runResult.Description,
                version = string.IsNullOrEmpty(config.MilestoneId) ? null : config.MilestoneId,
                testPlanKey = string.IsNullOrEmpty(config.PlanId) ? null : config.PlanId,
                testEnvironments = string.IsNullOrEmpty(config.ConfigurationId) ? null : new[] { config.ConfigurationId }
            },
            tests = new[]
            {
                new
                {
                    testKey = runResult.TestCaseId,
                    status = runResult.Status.ToUpperInvariant() == "PASSED" ? "PASSED" : "FAILED",
                    comment = runResult.Description,
                    start = runResult.StartTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    finish = runResult.EndTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    steps = runResult.Steps.Select(s => new
                    {
                        status = s.Status.ToUpperInvariant() == "PASSED" ? "PASSED" : "FAILED",
                        comment = s.ErrorMessage ?? s.StepLog ?? "",
                        actualResult = s.Status == "Passed" ? "Successful execution" : s.ErrorMessage
                    })
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("import/execution", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("No execution ID returned.");
    }

    public async Task<List<MetadataItem>> GetMilestonesAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        // For Xray, versions are read from Jira project versions
        using var client = CreateClient(config);
        var token = await AuthenticateAsync(client, config, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"jira/projects/{config.ProjectId}/versions", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("id").GetString() ?? "",
                Name = element.GetProperty("name").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetPlansAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var token = await AuthenticateAsync(client, config, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"jira/projects/{config.ProjectId}/testplans", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("key").GetString() ?? "",
                Name = element.GetProperty("summary").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetConfigurationsAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var token = await AuthenticateAsync(client, config, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"jira/projects/{config.ProjectId}/environments", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
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
