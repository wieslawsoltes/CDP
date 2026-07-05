using System;
using System.Threading.Tasks;
using OpenQA.Selenium.Appium.Android;
using Xunit;

namespace CDP.Automation.Appium;

public class CdpAppiumFixture : IAsyncLifetime
{
    private CdpAppiumOrchestrator? _orchestrator;

    public AndroidDriver? Driver => _orchestrator?.Driver;

    protected virtual CdpAppiumOptions GetOptions()
    {
        return new CdpAppiumOptions();
    }

    public async ValueTask InitializeAsync()
    {
        var options = GetOptions();
        _orchestrator = new CdpAppiumOrchestrator(options);
        await _orchestrator.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_orchestrator != null)
        {
            await _orchestrator.DisposeAsync();
        }
    }
}
