using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CDP.Selenium.Tests;

public class SeleniumTests : IAsyncLifetime
{
    private static ChromeDriver? _sharedDriver;
    private static readonly object _lock = new object();
    private static int _activeTestCount = 0;

    private ChromeDriver? _driver;

    public async ValueTask InitializeAsync()
    {
        lock (_lock)
        {
            _activeTestCount++;
            if (_sharedDriver == null)
            {
                // 1. Configure ChromeOptions to attach to the already running CdpSampleApp CDP port 9222
                var options = new ChromeOptions();
                options.DebuggerAddress = "127.0.0.1:9222";
                
                // Run headlessly and pass standard clean options
                options.AddArgument("--headless");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--disable-features=DevToolsTabTarget");

                try
                {
                    var service = ChromeDriverService.CreateDefaultService();
                    service.EnableVerboseLogging = true;
                    service.LogPath = Path.Combine(Directory.GetCurrentDirectory(), "chromedriver.log");
                    _sharedDriver = new ChromeDriver(service, options);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SELENIUM INIT ERROR] Failed to start ChromeDriver: {ex.Message}");
                    throw;
                }
            }
            _driver = _sharedDriver;
        }
        
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _activeTestCount--;
            if (_activeTestCount == 0 && _sharedDriver != null)
            {
                try
                {
                    _sharedDriver.Quit();
                    _sharedDriver.Dispose();
                }
                catch { }
                _sharedDriver = null;
            }
        }
        await Task.CompletedTask;
    }

    [Fact]
    public void VerifyHomePageElementsAndInteraction()
    {
        Assert.NotNull(_driver);
        Assert.Equal("Avalonia CDP Inspector Sample", _driver.Title);

        // Reset Home Tab select index
        _driver.ExecuteScript("document.querySelector('#tabContainer').selectedIndex = 0;");
        _driver.ExecuteScript("const status = document.querySelector('#txtStatus'); if (status) status.textContent = 'Not Clicked';");
        _driver.ExecuteScript("if (typeof Window !== 'undefined' && Window) Window.clickCount = 0;");

        var statusText = _driver.FindElement(By.Id("txtStatus"));
        Assert.Equal("Not Clicked", statusText.Text);

        var clickBtn = _driver.FindElement(By.Id("btnClickMe"));
        clickBtn.Click();
        Assert.Equal("Clicked 1 times!", statusText.Text);

        clickBtn.Click();
        Assert.Equal("Clicked 2 times!", statusText.Text);
    }

    [Fact]
    public void VerifyTextBoxInputAndBinding()
    {
        Assert.NotNull(_driver);
        var txtInput = _driver.FindElement(By.Id("txtInput"));
        txtInput.Clear();
        txtInput.SendKeys("CDP Selenium E2E!");
        Assert.Equal("CDP Selenium E2E!", txtInput.GetAttribute("value"));
    }

    [Fact]
    public void VerifySliderAndCheckBoxControls()
    {
        Assert.NotNull(_driver);
        var chkToggle = _driver.FindElement(By.Id("chkToggle"));
        
        // Check standard toggling
        if (chkToggle.Selected)
        {
            chkToggle.Click();
        }
        Assert.False(chkToggle.Selected);
        
        chkToggle.Click();
        Assert.True(chkToggle.Selected);

        // Update Slider value via Javascript evaluation
        _driver.ExecuteScript("document.querySelector('#sliderValue').value = 75;");
        
        var sliderText = _driver.FindElement(By.Id("txtSliderVal"));
        Assert.Equal("Slider Value: 75", sliderText.Text);
    }

    [Fact]
    public void VerifyNavigationBetweenTabs()
    {
        Assert.NotNull(_driver);
        
        // Navigate using Javascript selectContainer trigger index
        _driver.ExecuteScript("document.querySelector('#tabContainer').selectedIndex = 1;");
        var scrollContainer = _driver.FindElement(By.Id("scrollContainer"));
        Assert.True(scrollContainer.Displayed);

        _driver.ExecuteScript("document.querySelector('#tabContainer').selectedIndex = 2;");
        var aboutTitle = _driver.FindElement(By.Id("tabAbout"));
        Assert.True(aboutTitle.Displayed);
    }
}
