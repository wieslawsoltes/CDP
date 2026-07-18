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

namespace CDP.Integration.Qase;

public class QaseService : ITestService
{
    public string ServiceName => "Qase";

    private HttpClient CreateClient(TestServiceConfig config)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Url.TrimEnd('/') + "/v1/");
        client.DefaultRequestHeaders.Add("Token", config.Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<bool> ValidateConnectionAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        try
        {
            var response = await client.GetAsync("project", cancellationToken);
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
            steps = testCase.Steps.Select((s, i) => new
            {
                position = i + 1,
                action = s.Action,
                expected_result = s.Value ?? ""
            })
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"case/{config.ProjectId}", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").GetProperty("id").GetInt32().ToString();
    }

    public async Task<string> PublishRunResultAsync(TestServiceConfig config, TestRunResultData runResult, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);

        // 1. Create a run
        var runPayload = new
        {
            title = runResult.TestName,
            description = runResult.Description,
            cases = new[] { int.Parse(runResult.TestCaseId) },
            milestone_id = string.IsNullOrEmpty(config.MilestoneId) ? null : (int?)int.Parse(config.MilestoneId),
            plan_id = string.IsNullOrEmpty(config.PlanId) ? null : (int?)int.Parse(config.PlanId)
        };
        var runContent = new StringContent(JsonSerializer.Serialize(runPayload), Encoding.UTF8, "application/json");
        var runResponse = await client.PostAsync($"run/{config.ProjectId}", runContent, cancellationToken);
        runResponse.EnsureSuccessStatusCode();

        var runJson = await runResponse.Content.ReadAsStringAsync(cancellationToken);
        using var runDoc = JsonDocument.Parse(runJson);
        var runId = runDoc.RootElement.GetProperty("result").GetProperty("id").GetInt32().ToString();

        // 2. Upload PDF attachment if exists
        string? attachmentHash = null;
        if (!string.IsNullOrEmpty(runResult.PdfReportPath) && File.Exists(runResult.PdfReportPath))
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(runResult.PdfReportPath);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(streamContent, "file", Path.GetFileName(runResult.PdfReportPath));

                var attachmentResponse = await client.PostAsync($"attachment/{config.ProjectId}", form, cancellationToken);
                if (attachmentResponse.IsSuccessStatusCode)
                {
                    var attachJson = await attachmentResponse.Content.ReadAsStringAsync(cancellationToken);
                    using var attachDoc = JsonDocument.Parse(attachJson);
                    var resultElement = attachDoc.RootElement.GetProperty("result");
                    if (resultElement.ValueKind == JsonValueKind.Array && resultElement.GetArrayLength() > 0)
                    {
                        attachmentHash = resultElement[0].GetProperty("hash").GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Qase] Failed to upload PDF report attachment: {ex.Message}");
            }
        }

        // 3. Submit result (with step results)
        var resultPayload = new
        {
            case_id = int.Parse(runResult.TestCaseId),
            status = runResult.Status.ToLowerInvariant() == "passed" ? "passed" : "failed",
            time_ms = (long)runResult.DurationMs,
            comment = runResult.Description,
            attachments = attachmentHash != null ? new[] { attachmentHash } : Array.Empty<string>(),
            steps = runResult.Steps.Select(s => new
            {
                position = s.Index + 1,
                status = s.Status.ToLowerInvariant() == "passed" ? "passed" : "failed",
                comment = s.ErrorMessage ?? s.StepLog ?? $"Step {s.Action} completed."
            })
        };
        var resultContent = new StringContent(JsonSerializer.Serialize(resultPayload), Encoding.UTF8, "application/json");
        var resultResponse = await client.PostAsync($"result/{config.ProjectId}/{runId}", resultContent, cancellationToken);
        resultResponse.EnsureSuccessStatusCode();

        var resultJson = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
        using var resultDoc = JsonDocument.Parse(resultJson);
        return resultDoc.RootElement.GetProperty("result").GetProperty("hash").GetString() ?? throw new InvalidOperationException("No result hash returned.");
    }

    public async Task<List<MetadataItem>> GetMilestonesAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"milestone/{config.ProjectId}?limit=100", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        var resultElement = doc.RootElement.GetProperty("result").GetProperty("entities");
        foreach (var element in resultElement.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("id").GetInt32().ToString(),
                Name = element.GetProperty("title").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetPlansAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"plan/{config.ProjectId}?limit=100", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        var resultElement = doc.RootElement.GetProperty("result").GetProperty("entities");
        foreach (var element in resultElement.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("id").GetInt32().ToString(),
                Name = element.GetProperty("title").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetConfigurationsAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"configuration/{config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        var resultElement = doc.RootElement.GetProperty("result");
        foreach (var element in resultElement.EnumerateArray())
        {
            // Qase returns configuration groups. Each group has a list of configurations
            var title = element.GetProperty("title").GetString();
            var configs = element.GetProperty("configurations");
            foreach (var cfg in configs.EnumerateArray())
            {
                items.Add(new MetadataItem
                {
                    Id = cfg.GetProperty("id").GetInt32().ToString(),
                    Name = $"{title} - {cfg.GetProperty("title").GetString()}"
                });
            }
        }
        return items;
    }
}
