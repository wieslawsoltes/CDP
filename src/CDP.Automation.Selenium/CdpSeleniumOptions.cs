using System;

namespace CDP.Automation.Selenium;

public class CdpSeleniumOptions
{
    public string AppPath { get; set; } = "";
    public string AppArguments { get; set; } = "";
    public int AppCdpPort { get; set; } = 9222;
    public bool Headless { get; set; } = true;
    public bool StartApp { get; set; } = true;
    public string ChromeDriverLogPath { get; set; } = "";
    public bool EnableVerboseLogging { get; set; } = true;
}
