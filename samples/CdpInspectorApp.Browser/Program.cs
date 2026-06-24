using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace CdpInspectorApp.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        System.AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Console.WriteLine($"[GLOBAL UNHANDLED EXCEPTION]: {e.ExceptionObject}");
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Console.WriteLine($"[GLOBAL UNOBSERVED TASK EXCEPTION]: {e.Exception}");
        };

        if (args != null && args.Length > 0)
        {
            App.StartupUrl = args[0];
        }

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .LogToTrace();
}
