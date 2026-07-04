using System;
using System.IO;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CDP.Selenium.Tests;

public class SeleniumFixture : IAsyncLifetime
{
    public ChromeDriver? Driver { get; private set; }
    private System.Diagnostics.Process? _sampleAppProcess;

    private static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
            if (!success) return false;
            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartSampleApp()
    {
        string baseDir = AppContext.BaseDirectory;
        string config = baseDir.Contains("Release") ? "Release" : "Debug";

        string dllPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "samples", "CdpSampleApp", "bin", config, "net10.0", "CdpSampleApp.dll"));
        if (!File.Exists(dllPath))
        {
            dllPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "CdpSampleApp", "bin", config, "net10.0", "CdpSampleApp.dll"));
        }

        if (!File.Exists(dllPath))
        {
            dllPath = Path.Combine(Directory.GetCurrentDirectory(), "samples", "CdpSampleApp", "CdpSampleApp.csproj");
            var fallbackStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"dotnet run --project \\\"{dllPath}\\\" -- --headless > cdp-sample-app.log 2>&1\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _sampleAppProcess = new System.Diagnostics.Process { StartInfo = fallbackStartInfo };
            _sampleAppProcess.Start();
            return;
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"dotnet \\\"{dllPath}\\\" --headless > cdp-sample-app.log 2>&1\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _sampleAppProcess = new System.Diagnostics.Process { StartInfo = startInfo };
        _sampleAppProcess.Start();
    }

    public async ValueTask InitializeAsync()
    {
        bool isAlreadyRunning = IsPortOpen("127.0.0.1", 9222);
        if (!isAlreadyRunning)
        {
            if (_sampleAppProcess == null)
            {
                StartSampleApp();
            }

            for (int i = 0; i < 100; i++)
            {
                if (IsPortOpen("127.0.0.1", 9222)) break;
                if (_sampleAppProcess != null && _sampleAppProcess.HasExited)
                {
                    throw new Exception($"Sample app process exited prematurely with exit code {_sampleAppProcess.ExitCode}. Port 9222 never opened.");
                }
                await Task.Delay(100);
            }
            
            // Allow headless layout and arrange pass to settle
            await Task.Delay(1500);
        }

        // Configure ChromeOptions
        var options = new ChromeOptions();
        options.DebuggerAddress = "127.0.0.1:9222";
        
        options.AddArgument("--headless");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-features=DevToolsTabTarget");

        try
        {
            var service = ChromeDriverService.CreateDefaultService();
            service.EnableVerboseLogging = true;
            service.LogPath = Path.Combine(Directory.GetCurrentDirectory(), "chromedriver.log");
            Driver = new ChromeDriver(service, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SELENIUM INIT ERROR] Failed to start ChromeDriver: {ex.Message}");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Driver != null)
        {
            try
            {
                Driver.Quit();
                Driver.Dispose();
            }
            catch { }
            Driver = null;
        }

        if (_sampleAppProcess != null && !_sampleAppProcess.HasExited)
        {
            try
            {
                _sampleAppProcess.Kill(entireProcessTree: true);
                _sampleAppProcess.WaitForExit(2000);
            }
            catch { }
            if (!_sampleAppProcess.HasExited)
            {
                try
                {
                    _sampleAppProcess.Kill();
                    _sampleAppProcess.WaitForExit(1000);
                }
                catch { }
            }
            _sampleAppProcess.Dispose();
            _sampleAppProcess = null;
        }
        await Task.CompletedTask;
    }
}

public class SeleniumTests : IClassFixture<SeleniumFixture>
{
    private readonly SeleniumFixture _fixture;
    private readonly ChromeDriver _driver;

    public SeleniumTests(SeleniumFixture fixture)
    {
        _fixture = fixture;
        _driver = fixture.Driver ?? throw new InvalidOperationException("Driver was not initialized in fixture.");
    }

    [Fact]
    public void VerifyHomePageElementsAndInteraction()
    {
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
        var txtInput = _driver.FindElement(By.Id("txtInput"));
        txtInput.Clear();
        txtInput.SendKeys("CDP Selenium E2E!");
        Assert.Equal("CDP Selenium E2E!", txtInput.GetAttribute("value"));
    }

    [Fact]
    public void VerifySliderAndCheckBoxControls()
    {
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
        // Navigate using Javascript selectContainer trigger index
        _driver.ExecuteScript("document.querySelector('#tabContainer').selectedIndex = 1;");
        var scrollContainer = _driver.FindElement(By.Id("scrollContainer"));
        Assert.True(scrollContainer.Displayed);

        _driver.ExecuteScript("document.querySelector('#tabContainer').selectedIndex = 2;");
        var aboutTitle = _driver.FindElement(By.Id("tabAbout"));
        Assert.True(aboutTitle.Displayed);
    }
}
