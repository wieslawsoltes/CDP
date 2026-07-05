using System;

namespace CDP.Automation.Appium;

public class CdpAppiumOptions
{
    public string AppPath { get; set; } = "";
    public string AppArguments { get; set; } = "";
    public int AppCdpPort { get; set; } = 9222;
    public int AppiumPort { get; set; } = 4723;
    public string AppiumDriverScriptPath { get; set; } = "";
    public bool Headless { get; set; } = true;
    public bool StartApp { get; set; } = true;
    public bool StartDriver { get; set; } = true;
    public string AppiumServerUri { get; set; } = "http://127.0.0.1:4723/";
    public string PlatformName { get; set; } = "Android";
    public string AutomationName { get; set; } = "CDP";
}
