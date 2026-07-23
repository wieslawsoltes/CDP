using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.UI.Xaml;

namespace UnoSampleApp;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        try
        {
            var asm = Assembly.Load("Uno.UI.Runtime.Skia.MacOS");
            var builderType = asm.GetType("Uno.UI.Runtime.Skia.HostBuilder");
            if (builderType != null)
            {
                var createMethod = builderType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                var builder = createMethod?.Invoke(null, null);

                if (builder != null)
                {
                    var appMethod = builder.GetType().GetMethod("App");
                    builder = appMethod?.Invoke(builder, new object[] { new Func<Application>(() => new App()) });

                    var useMacMethod = builder?.GetType().GetMethod("UseMacOS");
                    if (useMacMethod != null)
                    {
                        var pars = useMacMethod.GetParameters();
                        if (pars.Length > 0)
                        {
                            var actionType = typeof(Action<>).MakeGenericType(pars[0].ParameterType);
                            var param = Expression.Parameter(pars[0].ParameterType, "opt");
                            var lambda = Expression.Lambda(actionType, Expression.Empty(), param);
                            var configureDelegate = lambda.Compile();

                            builder = useMacMethod.Invoke(builder, new object[] { configureDelegate });
                        }
                    }

                    var buildMethod = builder?.GetType().GetMethod("Build");
                    var host = buildMethod?.Invoke(builder, null);

                    var runMethod = host?.GetType().GetMethod("Run");
                    runMethod?.Invoke(host, null);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skia macOS host EXCEPTION: {ex.InnerException ?? ex}");
        }
    }
}
