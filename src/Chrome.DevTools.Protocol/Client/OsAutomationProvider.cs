using System;
using CDP.Automation.OS;

namespace Chrome.DevTools.Protocol;

public static class OsAutomationProvider
{
    public static IOsAutomation? Instance { get; set; }
}
