using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Threading;

namespace CDP.HeadlessRunner;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: cdp-runner <TargetAssemblyPath> [Port]");
            Console.WriteLine("Example: cdp-runner samples/CdpSampleApp/bin/Debug/net10.0/CdpSampleApp.dll 9222");
            return;
        }

        string assemblyPath = args[0];
        int port = 9222;
        if (args.Length >= 2 && int.TryParse(args[1], out int parsedPort))
        {
            port = parsedPort;
        }

        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine($"Error: Target assembly not found at '{assemblyPath}'");
            return;
        }

        string fullAssemblyPath = Path.GetFullPath(assemblyPath);
        string? assemblyDir = Path.GetDirectoryName(fullAssemblyPath);

        // Register assembly resolution for adjacent dependencies
        AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
        {
            var assemblyName = new AssemblyName(resolveArgs.Name).Name;
            if (assemblyName == null) return null;
            
            // Check if already loaded in the AppDomain
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (loadedAssembly != null) return loadedAssembly;

            var path = Path.Combine(assemblyDir!, assemblyName + ".dll");
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }
            
            var exePath = Path.Combine(assemblyDir!, assemblyName + ".exe");
            if (File.Exists(exePath))
            {
                return Assembly.LoadFrom(exePath);
            }

            return null;
        };

        Console.WriteLine($"Loading target assembly: {fullAssemblyPath}");
        Assembly targetAssembly;
        try
        {
            targetAssembly = Assembly.LoadFrom(fullAssemblyPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading assembly: {ex.Message}");
            return;
        }

        // Find Avalonia.Application type
        Type? appType = null;
        try
        {
            foreach (var type in targetAssembly.GetTypes())
            {
                if (typeof(Application).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    appType = type;
                    break;
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            Console.WriteLine("Warning: Some types could not be loaded. Searching loader exceptions...");
            foreach (var loaderEx in ex.LoaderExceptions)
            {
                Console.WriteLine($"Loader exception: {loaderEx?.Message}");
            }
            appType = ex.Types.FirstOrDefault(t => t != null && typeof(Application).IsAssignableFrom(t) && !t.IsAbstract);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning types: {ex.Message}");
            return;
        }

        if (appType == null)
        {
            Console.WriteLine("Error: Could not find any non-abstract class inheriting from Avalonia.Application in the target assembly.");
            return;
        }

        Console.WriteLine($"Found Application type: {appType.FullName}");

        // Build and configure the app
        AppBuilder? builder = null;

        // First look for BuildAvaloniaApp() static method
        MethodInfo? buildAppMethod = null;
        try
        {
            foreach (var type in targetAssembly.GetTypes())
            {
                var method = type.GetMethod("BuildAvaloniaApp", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && typeof(AppBuilder).IsAssignableFrom(method.ReturnType))
                {
                    buildAppMethod = method;
                    break;
                }
            }
        }
        catch (Exception) { /* ignore and fallback */ }

        if (buildAppMethod != null)
        {
            Console.WriteLine($"Invoking BuildAvaloniaApp from type {buildAppMethod.DeclaringType?.FullName}");
            try
            {
                builder = (AppBuilder)buildAppMethod.Invoke(null, null)!;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to invoke BuildAvaloniaApp: {ex.Message}. Falling back to default configuration.");
            }
        }

        if (builder == null)
        {
            Console.WriteLine("Configuring AppBuilder dynamically using reflection...");
            var configureMethod = typeof(AppBuilder).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Configure" && m.IsGenericMethod && m.GetParameters().Length == 0);
            if (configureMethod != null)
            {
                var genericConfigure = configureMethod.MakeGenericMethod(appType);
                builder = (AppBuilder)genericConfigure.Invoke(null, null)!;
            }
        }

        if (builder == null)
        {
            Console.WriteLine("Error: Could not configure AppBuilder.");
            return;
        }

        // Configure headless platform
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions());

        Console.WriteLine("Setting up application...");
        builder.SetupWithoutStarting();

        CdpServer.EnsureInitialized();

        // Find Window type
        Type? windowType = null;
        try
        {
            foreach (var type in targetAssembly.GetTypes())
            {
                if (typeof(Window).IsAssignableFrom(type) && !type.IsAbstract && type.Name == "MainWindow")
                {
                    windowType = type;
                    break;
                }
            }
            if (windowType == null)
            {
                foreach (var type in targetAssembly.GetTypes())
                {
                    if (typeof(Window).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        windowType = type;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error scanning for Window types: {ex.Message}");
        }

        if (windowType == null)
        {
            Console.WriteLine("Error: Could not find any class inheriting from Avalonia.Controls.Window in the target assembly.");
            return;
        }

        Console.WriteLine($"Instantiating MainWindow: {windowType.FullName}");
        Window window;
        try
        {
            window = (Window)Activator.CreateInstance(windowType)!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error instantiating window: {ex.Message}");
            return;
        }

        window.Show();

        Console.WriteLine($"Starting CDP Server on port {port}...");
        CdpServer.Start(port: port);

        Console.WriteLine("Running headless message loop. Press Ctrl+C to terminate.");
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(16);
        }
    }
}
