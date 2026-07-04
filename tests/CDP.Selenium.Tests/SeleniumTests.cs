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

        System.Diagnostics.ProcessStartInfo startInfo;
        if (!File.Exists(dllPath))
        {
            dllPath = Path.Combine(Directory.GetCurrentDirectory(), "samples", "CdpSampleApp", "CdpSampleApp.csproj");
            startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{dllPath}\" -- --headless",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }
        else
        {
            startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\" --headless",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        _sampleAppProcess = new System.Diagnostics.Process { StartInfo = startInfo };
        _sampleAppProcess.Start();

        // Asynchronously consume stdout and write to log file to avoid process hang/buffer overflow
        Task.Run(async () =>
        {
            try
            {
                using var writer = new StreamWriter(new FileStream("cdp-sample-app.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                while (!_sampleAppProcess.HasExited)
                {
                    var line = await _sampleAppProcess.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        await writer.WriteLineAsync(line);
                        await writer.FlushAsync();
                    }
                    else
                    {
                        await Task.Delay(50);
                    }
                }
            }
            catch {}
        });
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

    private void SelectTab(int index)
    {
        _driver.ExecuteScript($"document.querySelector('#tabContainer').selectedIndex = {index};");
        string checkScript = index switch
        {
            1 => "const el = document.querySelector('#scrollContainer'); return !!(el && el.offsetWidth > 0);",
            2 => "const el = document.querySelector('#btnGoBack'); return !!(el && el.offsetWidth > 0);",
            _ => "const el = document.querySelector('#txtInput'); return !!(el && el.offsetWidth > 0);"
        };

        for (int i = 0; i < 30; i++)
        {
            var isSettled = _driver.ExecuteScript(checkScript) as bool? ?? false;
            if (isSettled) return;
            System.Threading.Thread.Sleep(100);
        }
    }

    [Fact]
    public void VerifyHomePageElementsAndInteraction()
    {
        Assert.Equal("Avalonia CDP Inspector Sample", _driver.Title);

        // Reset Home Tab select index
        SelectTab(0);
        _driver.ExecuteScript("const status = document.querySelector('#txtStatus'); if (status) status.textContent = 'Not Clicked';");
        _driver.ExecuteScript("if (typeof Window !== 'undefined' && Window) Window.clickCount = 0;");

        var statusText = _driver.FindElement(By.Id("txtStatus"));
        for (int i = 0; i < 50; i++)
        {
            if (statusText.Text == "Not Clicked") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Not Clicked", statusText.Text);

        var clickBtn = _driver.FindElement(By.Id("btnClickMe"));
        clickBtn.Click();
        for (int i = 0; i < 50; i++)
        {
            if (statusText.Text == "Clicked 1 times!") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Clicked 1 times!", statusText.Text);

        clickBtn.Click();
        for (int i = 0; i < 50; i++)
        {
            if (statusText.Text == "Clicked 2 times!") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Clicked 2 times!", statusText.Text);
    }

    [Fact]
    public void VerifyTextBoxInputAndBinding()
    {
        SelectTab(0);
        var txtInput = _driver.FindElement(By.Id("txtInput"));
        txtInput.Clear();
        txtInput.SendKeys("CDP Selenium E2E!");
        for (int i = 0; i < 50; i++)
        {
            if (txtInput.GetAttribute("value") == "CDP Selenium E2E!") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("CDP Selenium E2E!", txtInput.GetAttribute("value"));
    }

    [Fact]
    public void VerifySliderAndCheckBoxControls()
    {
        SelectTab(0);
        var chkToggle = _driver.FindElement(By.Id("chkToggle"));
        
        // Check standard toggling
        if (chkToggle.Selected)
        {
            chkToggle.Click();
        }
        for (int i = 0; i < 50; i++)
        {
            if (!chkToggle.Selected) break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.False(chkToggle.Selected);
        
        chkToggle.Click();
        for (int i = 0; i < 50; i++)
        {
            if (chkToggle.Selected) break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.True(chkToggle.Selected);

        // Update Slider value via Javascript evaluation
        _driver.ExecuteScript("document.querySelector('#sliderValue').value = 75;");
        
        var sliderText = _driver.FindElement(By.Id("txtSliderVal"));
        for (int i = 0; i < 50; i++)
        {
            if (sliderText.Text == "Slider Value: 75") break;
            System.Threading.Thread.Sleep(100);
        }
        Assert.Equal("Slider Value: 75", sliderText.Text);
    }

    [Fact]
    public void VerifyNavigationBetweenTabs()
    {
        // Navigate using Javascript selectContainer trigger index
        SelectTab(1);
        var scrollContainer = _driver.FindElement(By.Id("scrollContainer"));
        Assert.True(scrollContainer.Displayed);

        SelectTab(2);
        var aboutTitle = _driver.FindElement(By.Id("tabAbout"));
        Assert.True(aboutTitle.Displayed);
    }
}
