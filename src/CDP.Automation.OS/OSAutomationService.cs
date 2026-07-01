using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using CDP.Automation.OS.MacOs;
using CDP.Automation.OS.Windows;
using CDP.Automation.OS.Linux;

namespace CDP.Automation.OS;

public static class OSAutomationService
{
    private static readonly Lazy<IOsAutomation> _instance = new(() => CreateInstance());

    public static IOsAutomation Instance => _instance.Value;

    private static IOsAutomation CreateInstance(ILogger? logger = null)
    {
        IOsAutomation native;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            native = new MacOsAutomation(logger);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            native = new WindowsAutomation(logger);
        }
        else
        {
            native = new LinuxAutomation(logger);
        }
        return new EnrichedOsAutomation(native);
    }
}
