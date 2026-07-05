using System;
using System.Threading.Tasks;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace CDP.Automation.Selenium;

public class CdpSeleniumFixture : IAsyncLifetime
{
    private CdpSeleniumOrchestrator? _orchestrator;

    public ChromeDriver? Driver => _orchestrator?.Driver;

    protected virtual CdpSeleniumOptions GetOptions()
    {
        return new CdpSeleniumOptions();
    }

    public async ValueTask InitializeAsync()
    {
        var options = GetOptions();
        _orchestrator = new CdpSeleniumOrchestrator(options);
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
