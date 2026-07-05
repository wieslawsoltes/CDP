using System;
using System.Linq;
using System.Reflection;
using CDP.Automation.OS;

namespace Chrome.DevTools.Protocol;

public static class OsAutomationProvider
{
    private static IOsAutomation? _instance;
    public static IOsAutomation? Instance
    {
        get
        {
            if (_instance == null)
            {
                try
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "CDP.Automation.OS");
                    if (asm == null)
                    {
                        try
                        {
                            asm = Assembly.Load("CDP.Automation.OS");
                        }
                        catch {}
                    }
                    if (asm != null)
                    {
                        var type = asm.GetType("CDP.Automation.OS.OSAutomationService");
                        if (type != null)
                        {
                            var prop = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            if (prop != null)
                            {
                                _instance = prop.GetValue(null) as IOsAutomation;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore if OSAutomationService cannot be loaded or initialized
                }
            }
            return _instance;
        }
        set => _instance = value;
    }
}
