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

namespace CDP.Integration.TestMo;

public class TestMoService : ITestService
{
    public string ServiceName => "TestMo";

    private HttpClient CreateClient(TestServiceConfig config)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Url.TrimEnd('/') + "/api/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<bool> ValidateConnectionAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        try
        {
            var response = await client.GetAsync("projects", cancellationToken);
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
            title = testCase.Title,
            description = testCase.Description,
            tags = testCase.Tags,
            custom_steps = testCase.Steps.Select(s => new { action = s.Action, selector = s.Selector, value = s.Value })
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"projects/{config.ProjectId}/cases", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("No case ID returned.");
    }

    public async Task<string> PublishRunResultAsync(TestServiceConfig config, TestRunResultData runResult, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);

        // 1. Create a Test Run (with milestone & configurations mapping if provided)
        var runPayload = new
        {
            name = runResult.TestName,
            description = runResult.Description,
            source = "CDP Test Studio",
            milestone_id = string.IsNullOrEmpty(config.MilestoneId) ? null : config.MilestoneId,
            config_ids = string.IsNullOrEmpty(config.ConfigurationId) ? null : new[] { config.ConfigurationId }
        };
        var runContent = new StringContent(JsonSerializer.Serialize(runPayload), Encoding.UTF8, "application/json");
        var runResponse = await client.PostAsync($"projects/{config.ProjectId}/runs", runContent, cancellationToken);
        runResponse.EnsureSuccessStatusCode();

        var runJson = await runResponse.Content.ReadAsStringAsync(cancellationToken);
        using var runDoc = JsonDocument.Parse(runJson);
        var runId = runDoc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("No run ID returned.");

        // 2. Submit test result with step results
        var resultPayload = new
        {
            case_id = runResult.TestCaseId,
            status = runResult.Status.ToLowerInvariant() == "passed" ? "passed" : "failed",
            elapsed = (int)Math.Max(1, runResult.DurationMs / 1000.0),
            comment = runResult.Description,
            steps = runResult.Steps.Select(s => new
            {
                index = s.Index,
                status = s.Status.ToLowerInvariant() == "passed" ? "passed" : "failed",
                comment = s.ErrorMessage ?? s.StepLog ?? $"Step {s.Action} executed successfully.",
                elapsed_ms = s.DurationMs
            })
        };
        var resultContent = new StringContent(JsonSerializer.Serialize(resultPayload), Encoding.UTF8, "application/json");
        var resultResponse = await client.PostAsync($"runs/{runId}/results", resultContent, cancellationToken);
        resultResponse.EnsureSuccessStatusCode();

        var resultJson = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
        using var resultDoc = JsonDocument.Parse(resultJson);
        var resultId = resultDoc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("No result ID returned.");

        // 3. Upload PDF report if available
        if (!string.IsNullOrEmpty(runResult.PdfReportPath) && File.Exists(runResult.PdfReportPath))
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(runResult.PdfReportPath);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(streamContent, "file", Path.GetFileName(runResult.PdfReportPath));

                var attachmentResponse = await client.PostAsync($"results/{resultId}/attachments", form, cancellationToken);
                attachmentResponse.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestMo] Failed to upload PDF report attachment: {ex.Message}");
            }
        }

        return resultId;
    }

    public async Task<List<MetadataItem>> GetMilestonesAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"projects/{config.ProjectId}/milestones", cancellationToken);
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
        // TestMo uses runs or suites as mapping targets
        var response = await client.GetAsync($"projects/{config.ProjectId}/runs", cancellationToken);
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

    public async Task<List<MetadataItem>> GetConfigurationsAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"projects/{config.ProjectId}/configurations", cancellationToken);
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
}
