using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using CDP.Integration.Core;
using CDP.Integration.TestMo;
using CDP.Integration.TestRail;
using CDP.Integration.Qase;
using CDP.Integration.Xray;
using CDP.Integration.Zephyr;

namespace CDP.Integration.Tests;

public class IntegrationTests : IDisposable
{
    private readonly MockHttpServer _server;
    private readonly TestServiceConfig _config;

    public IntegrationTests()
    {
        _server = new MockHttpServer();
        _server.Start();

        _config = new TestServiceConfig
        {
            Url = _server.Url,
            Username = "test-user",
            Token = "test-token",
            ProjectId = "proj-1",
            SuiteId = "1"
        };
    }

    [Fact]
    public async Task TestMo_Integration_Flow()
    {
        var service = new TestMoService();

        // 1. Connection
        bool connected = await service.ValidateConnectionAsync(_config);
        Assert.True(connected);

        // 2. Query metadata
        var milestones = await service.GetMilestonesAsync(_config);
        Assert.NotEmpty(milestones);
        Assert.Equal("ms-1", milestones[0].Id);

        var plans = await service.GetPlansAsync(_config);
        Assert.NotEmpty(plans);
        Assert.Equal("plan-1", plans[0].Id);

        var configs = await service.GetConfigurationsAsync(_config);
        Assert.NotEmpty(configs);
        Assert.Equal("cfg-1", configs[0].Id);

        // 3. Export test case
        var testCase = new TestCaseData
        {
            Title = "Sample Test Case",
            Description = "A simple verification step",
            Steps = new List<TestStepData>
            {
                new() { Action = "tapOn", Selector = "#button" }
            }
        };
        string caseId = await service.ExportTestCaseAsync(_config, testCase);
        Assert.Equal("case-123", caseId);

        // 4. Publish results (including step results)
        var runResult = new TestRunResultData
        {
            TestCaseId = caseId,
            TestName = "Sample Test Studio Run",
            Description = "Success",
            Status = "Passed",
            DurationMs = 2500,
            StartTime = DateTime.UtcNow.AddSeconds(-3),
            EndTime = DateTime.UtcNow,
            Steps = new List<TestStepResultData>
            {
                new() { Index = 0, Action = "tapOn", Status = "Passed", DurationMs = 500 }
            }
        };
        string resultId = await service.PublishRunResultAsync(_config, runResult);
        Assert.Equal("result-789", resultId);
    }

    [Fact]
    public async Task TestRail_Integration_Flow()
    {
        var service = new TestRailService();

        // 1. Connection
        bool connected = await service.ValidateConnectionAsync(_config);
        Assert.True(connected);

        // 2. Query metadata
        var milestones = await service.GetMilestonesAsync(_config);
        Assert.NotEmpty(milestones);
        Assert.Equal("101", milestones[0].Id);

        var plans = await service.GetPlansAsync(_config);
        Assert.NotEmpty(plans);
        Assert.Equal("201", plans[0].Id);

        var configs = await service.GetConfigurationsAsync(_config);
        Assert.NotEmpty(configs);
        Assert.Equal("1", configs[0].Id);

        // 3. Export test case
        var testCase = new TestCaseData
        {
            Title = "Sample Test Case",
            Description = "A simple verification step",
            Steps = new List<TestStepData>
            {
                new() { Action = "tapOn", Selector = "#button" }
            }
        };
        string caseId = await service.ExportTestCaseAsync(_config, testCase);
        Assert.Equal("123", caseId);

        // 4. Publish results
        var runResult = new TestRunResultData
        {
            TestCaseId = caseId,
            TestName = "Sample Test Studio Run",
            Description = "Success",
            Status = "Passed",
            DurationMs = 2500,
            StartTime = DateTime.UtcNow.AddSeconds(-3),
            EndTime = DateTime.UtcNow,
            Steps = new List<TestStepResultData>
            {
                new() { Index = 0, Action = "tapOn", Status = "Passed", DurationMs = 500 }
            }
        };
        string resultId = await service.PublishRunResultAsync(_config, runResult);
        Assert.Equal("789", resultId);
    }

    [Fact]
    public async Task Qase_Integration_Flow()
    {
        var service = new QaseService();
        var qaseConfig = new TestServiceConfig
        {
            Url = _server.Url,
            Username = "test-user",
            Token = "test-token",
            ProjectId = "PRJ",
            SuiteId = "1"
        };

        // 1. Connection
        bool connected = await service.ValidateConnectionAsync(qaseConfig);
        Assert.True(connected);

        // 2. Query metadata
        var milestones = await service.GetMilestonesAsync(qaseConfig);
        Assert.NotEmpty(milestones);
        Assert.Equal("101", milestones[0].Id);

        var plans = await service.GetPlansAsync(qaseConfig);
        Assert.NotEmpty(plans);
        Assert.Equal("201", plans[0].Id);

        var configs = await service.GetConfigurationsAsync(qaseConfig);
        Assert.NotEmpty(configs);
        Assert.Equal("1", configs[0].Id);

        // 3. Export test case
        var testCase = new TestCaseData
        {
            Title = "Sample Test Case",
            Description = "A simple verification step",
            Steps = new List<TestStepData>
            {
                new() { Action = "tapOn", Selector = "#button" }
            }
        };
        string caseId = await service.ExportTestCaseAsync(qaseConfig, testCase);
        Assert.Equal("123", caseId);

        // 4. Publish results
        var runResult = new TestRunResultData
        {
            TestCaseId = caseId,
            TestName = "Sample Test Studio Run",
            Description = "Success",
            Status = "Passed",
            DurationMs = 2500,
            StartTime = DateTime.UtcNow.AddSeconds(-3),
            EndTime = DateTime.UtcNow,
            Steps = new List<TestStepResultData>
            {
                new() { Index = 0, Action = "tapOn", Status = "Passed", DurationMs = 500 }
            }
        };
        string resultId = await service.PublishRunResultAsync(qaseConfig, runResult);
        Assert.Equal("res-hash", resultId);
    }

    [Fact]
    public async Task Xray_Integration_Flow()
    {
        var service = new XrayService();
        var xrayConfig = new TestServiceConfig
        {
            Url = _server.Url,
            Username = "test-user",
            Token = "test-token",
            ProjectId = "PRJ",
            SuiteId = "1"
        };

        // 1. Connection
        bool connected = await service.ValidateConnectionAsync(xrayConfig);
        Assert.True(connected);

        // 2. Query metadata
        var milestones = await service.GetMilestonesAsync(xrayConfig);
        Assert.NotEmpty(milestones);
        Assert.Equal("v-1", milestones[0].Id);

        var plans = await service.GetPlansAsync(xrayConfig);
        Assert.NotEmpty(plans);
        Assert.Equal("plan-1", plans[0].Id);

        var configs = await service.GetConfigurationsAsync(xrayConfig);
        Assert.NotEmpty(configs);
        Assert.Equal("Env 1", configs[0].Id);

        // 3. Export test case
        var testCase = new TestCaseData
        {
            Title = "Sample Test Case",
            Description = "A simple verification step",
            Steps = new List<TestStepData>
            {
                new() { Action = "tapOn", Selector = "#button" }
            }
        };
        string caseId = await service.ExportTestCaseAsync(xrayConfig, testCase);
        Assert.Equal("TEST-123", caseId);

        // 4. Publish results
        var runResult = new TestRunResultData
        {
            TestCaseId = caseId,
            TestName = "Sample Test Studio Run",
            Description = "Success",
            Status = "Passed",
            DurationMs = 2500,
            StartTime = DateTime.UtcNow.AddSeconds(-3),
            EndTime = DateTime.UtcNow,
            Steps = new List<TestStepResultData>
            {
                new() { Index = 0, Action = "tapOn", Status = "Passed", DurationMs = 500 }
            }
        };
        string resultId = await service.PublishRunResultAsync(xrayConfig, runResult);
        Assert.Equal("EXEC-456", resultId);
    }

    [Fact]
    public async Task Zephyr_Integration_Flow()
    {
        var service = new ZephyrService();
        var zephyrConfig = new TestServiceConfig
        {
            Url = _server.Url,
            Username = "test-user",
            Token = "test-token",
            ProjectId = "PRJ",
            SuiteId = "1"
        };

        // 1. Connection
        bool connected = await service.ValidateConnectionAsync(zephyrConfig);
        Assert.True(connected);

        // 2. Query metadata
        var milestones = await service.GetMilestonesAsync(zephyrConfig);
        Assert.NotEmpty(milestones);
        Assert.Equal("cycle-1", milestones[0].Id);

        var plans = await service.GetPlansAsync(zephyrConfig);
        Assert.NotEmpty(plans);
        Assert.Equal("plan-1", plans[0].Id);

        var configs = await service.GetConfigurationsAsync(zephyrConfig);
        Assert.NotEmpty(configs);
        Assert.Equal("Env 1", configs[0].Id);

        // 3. Export test case
        var testCase = new TestCaseData
        {
            Title = "Sample Test Case",
            Description = "A simple verification step",
            Steps = new List<TestStepData>
            {
                new() { Action = "tapOn", Selector = "#button" }
            }
        };
        string caseId = await service.ExportTestCaseAsync(zephyrConfig, testCase);
        Assert.Equal("ZEP-123", caseId);

        // 4. Publish results
        var runResult = new TestRunResultData
        {
            TestCaseId = caseId,
            TestName = "Sample Test Studio Run",
            Description = "Success",
            Status = "Passed",
            DurationMs = 2500,
            StartTime = DateTime.UtcNow.AddSeconds(-3),
            EndTime = DateTime.UtcNow,
            Steps = new List<TestStepResultData>
            {
                new() { Index = 0, Action = "tapOn", Status = "Passed", DurationMs = 500 }
            }
        };
        string resultId = await service.PublishRunResultAsync(zephyrConfig, runResult);
        Assert.Equal("ZEP-RUN", resultId);
    }

    [Fact]
    public void Test_IntegrationsFacade_Script_Evaluation()
    {
        TestServiceRegistry.Register(new TestMoService());
        
        IntegrationsFacade.SetProvider("TestMo");
        IntegrationsFacade.Configure(_config.Url, _config.Username, _config.Token, _config.ProjectId);

        bool connected = IntegrationsFacade.VerifyConnection();
        Assert.True(connected);

        string caseId = IntegrationsFacade.ExportTestCase("Facade Case", "Facade Description");
        Assert.Equal("case-123", caseId);

        string resultId = IntegrationsFacade.PublishRunResult(caseId, "Facade Run", "Passed", 1500);
        Assert.Equal("result-789", resultId);
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}
