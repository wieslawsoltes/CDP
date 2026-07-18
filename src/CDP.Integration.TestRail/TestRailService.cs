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

namespace CDP.Integration.TestRail;

public class TestRailService : ITestService
{
    public string ServiceName => "TestRail";

    private HttpClient CreateClient(TestServiceConfig config)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Url.TrimEnd('/') + "/");
        
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Token}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<bool> ValidateConnectionAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        try
        {
            var response = await client.GetAsync("index.php?/api/v2/get_projects", cancellationToken);
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
        var sectionId = string.IsNullOrEmpty(config.SuiteId) ? "1" : config.SuiteId;

        var payload = new
        {
            title = testCase.Title,
            custom_preconds = testCase.Description,
            custom_steps_separated = testCase.Steps.Select(s => new { content = s.Action, expected = s.Value ?? "" })
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"index.php?/api/v2/add_case/{sectionId}", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetInt32().ToString();
    }

    public async Task<string> PublishRunResultAsync(TestServiceConfig config, TestRunResultData runResult, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        string runId;

        if (!string.IsNullOrEmpty(config.PlanId))
        {
            // If plan ID is provided, add entry to plan
            var entryPayload = new
            {
                suite_id = string.IsNullOrEmpty(config.SuiteId) ? 1 : int.Parse(config.SuiteId),
                name = runResult.TestName,
                include_all = true
            };
            var entryContent = new StringContent(JsonSerializer.Serialize(entryPayload), Encoding.UTF8, "application/json");
            var entryResponse = await client.PostAsync($"index.php?/api/v2/add_plan_entry/{config.PlanId}", entryContent, cancellationToken);
            entryResponse.EnsureSuccessStatusCode();

            var entryJson = await entryResponse.Content.ReadAsStringAsync(cancellationToken);
            using var entryDoc = JsonDocument.Parse(entryJson);
            // Get run ID from first run in the entry
            var runsArray = entryDoc.RootElement.GetProperty("runs");
            if (runsArray.ValueKind == JsonValueKind.Array && runsArray.GetArrayLength() > 0)
            {
                runId = runsArray[0].GetProperty("id").GetInt32().ToString();
            }
            else
            {
                throw new InvalidOperationException("No runs created under the plan entry.");
            }
        }
        else
        {
            // Create a standalone run
            var runPayload = new
            {
                suite_id = string.IsNullOrEmpty(config.SuiteId) ? 1 : int.Parse(config.SuiteId),
                name = runResult.TestName,
                description = runResult.Description,
                include_all = true,
                milestone_id = string.IsNullOrEmpty(config.MilestoneId) ? null : (int?)int.Parse(config.MilestoneId)
            };
            var runContent = new StringContent(JsonSerializer.Serialize(runPayload), Encoding.UTF8, "application/json");
            var runResponse = await client.PostAsync($"index.php?/api/v2/add_run/{config.ProjectId}", runContent, cancellationToken);
            runResponse.EnsureSuccessStatusCode();

            var runJson = await runResponse.Content.ReadAsStringAsync(cancellationToken);
            using var runDoc = JsonDocument.Parse(runJson);
            runId = runDoc.RootElement.GetProperty("id").GetInt32().ToString();
        }

        // 2. Add Result for case (with step results)
        var statusId = runResult.Status.ToLowerInvariant() == "passed" ? 1 : 5; // 1 = Passed, 5 = Failed
        var stepResults = runResult.Steps.Select(s => new
        {
            status_id = s.Status.ToLowerInvariant() == "passed" ? 1 : 5,
            comment = s.ErrorMessage ?? s.StepLog ?? $"Step {s.Action} completed successfully.",
            actual = s.Status,
            elapsed = $"{(int)Math.Max(1, s.DurationMs / 1000.0)}s"
        });

        var resultPayload = new
        {
            status_id = statusId,
            comment = runResult.Description,
            elapsed = $"{(int)Math.Max(1, runResult.DurationMs / 1000.0)}s",
            custom_step_results = stepResults
        };
        var resultContent = new StringContent(JsonSerializer.Serialize(resultPayload), Encoding.UTF8, "application/json");
        var resultResponse = await client.PostAsync($"index.php?/api/v2/add_result_for_case/{runId}/{runResult.TestCaseId}", resultContent, cancellationToken);
        resultResponse.EnsureSuccessStatusCode();

        var resultJson = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
        using var resultDoc = JsonDocument.Parse(resultJson);
        var resultId = resultDoc.RootElement.GetProperty("id").GetInt32().ToString();

        // 3. Attach report if available
        if (!string.IsNullOrEmpty(runResult.PdfReportPath) && File.Exists(runResult.PdfReportPath))
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(runResult.PdfReportPath);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(streamContent, "attachments", Path.GetFileName(runResult.PdfReportPath));

                var attachmentResponse = await client.PostAsync($"index.php?/api/v2/add_attachment_to_result/{resultId}", form, cancellationToken);
                attachmentResponse.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestRail] Failed to upload PDF report attachment: {ex.Message}");
            }
        }

        return resultId;
    }

    public async Task<List<MetadataItem>> GetMilestonesAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"index.php?/api/v2/get_milestones/{config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("id").GetInt32().ToString(),
                Name = element.GetProperty("name").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetPlansAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"index.php?/api/v2/get_plans/{config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            items.Add(new MetadataItem
            {
                Id = element.GetProperty("id").GetInt32().ToString(),
                Name = element.GetProperty("name").GetString() ?? ""
            });
        }
        return items;
    }

    public async Task<List<MetadataItem>> GetConfigurationsAsync(TestServiceConfig config, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(config);
        var response = await client.GetAsync($"index.php?/api/v2/get_configs/{config.ProjectId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return new List<MetadataItem>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var items = new List<MetadataItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            // TestRail configs are returned in config groups
            var configs = element.GetProperty("configs");
            foreach (var cfg in configs.EnumerateArray())
            {
                items.Add(new MetadataItem
                {
                    Id = cfg.GetProperty("id").GetInt32().ToString(),
                    Name = $"{element.GetProperty("name").GetString()} - {cfg.GetProperty("name").GetString()}"
                });
            }
        }
        return items;
    }
}
