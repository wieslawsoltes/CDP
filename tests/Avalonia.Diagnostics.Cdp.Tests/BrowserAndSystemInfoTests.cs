using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Cdp.Domains;
using BrowserDomain = Avalonia.Diagnostics.Cdp.Domains.BrowserDomain;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class BrowserAndSystemInfoTests
{
    [Fact]
    public async Task TestBrowserDomainGetVersion()
    {
        var result = await BrowserDomain.HandleAsync(null!, "getVersion", new JsonObject());
        
        Assert.NotNull(result);
        Assert.Equal("1.3", result["protocolVersion"]?.GetValue<string>());
        Assert.Equal("Avalonia/11.3.12", result["product"]?.GetValue<string>());
        Assert.Equal("1.0", result["revision"]?.GetValue<string>());
        Assert.StartsWith("Mozilla/5.0", result["userAgent"]?.GetValue<string>());
        Assert.Equal(".NET 10.0", result["jsVersion"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestBrowserDomainClose()
    {
        // Should not throw and should return empty JsonObject since in unit test headless lifetime isn't IClassicDesktopStyleApplicationLifetime
        var result = await BrowserDomain.HandleAsync(null!, "close", new JsonObject());
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task TestSystemInfoDomainGetInfo()
    {
        var result = await SystemInfoDomain.HandleAsync(null!, "getInfo", new JsonObject());
        
        Assert.NotNull(result);
        
        var gpu = result["gpu"] as JsonObject;
        Assert.NotNull(gpu);
        
        var devices = gpu["devices"] as JsonArray;
        Assert.NotNull(devices);
        Assert.NotEmpty(devices);
        
        var firstDevice = devices[0] as JsonObject;
        Assert.NotNull(firstDevice);
        Assert.Contains("vendorId", firstDevice);
        Assert.Contains("deviceId", firstDevice);

        Assert.NotEmpty(result["modelName"]?.GetValue<string>() ?? "");
        Assert.NotEmpty(result["modelVersion"]?.GetValue<string>() ?? "");
        Assert.NotEmpty(result["commandLine"]?.GetValue<string>() ?? "");
    }

    [Fact]
    public async Task TestSystemInfoDomainGetProcessInfo()
    {
        var result = await SystemInfoDomain.HandleAsync(null!, "getProcessInfo", new JsonObject());
        
        Assert.NotNull(result);
        
        var processInfo = result["processInfo"] as JsonArray;
        Assert.NotNull(processInfo);
        Assert.Single(processInfo);
        
        var processObj = processInfo[0] as JsonObject;
        Assert.NotNull(processObj);
        
        Assert.True(processObj["id"]?.GetValue<int>() > 0);
        Assert.Equal("browser", processObj["type"]?.GetValue<string>());
        Assert.True(processObj["cpuTime"]?.GetValue<double>() >= 0);
        Assert.True(processObj["workingSet"]?.GetValue<long>() > 0);
        Assert.True(processObj["threadCount"]?.GetValue<int>() > 0);
    }
}
