using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using ReactiveUI;

[assembly: AvaloniaTestApplication(typeof(CDP.Rdp.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace CDP.Rdp.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class TestApp : Application
{
    public override void Initialize()
    {
        RxApp.MainThreadScheduler = DispatcherScheduler.Current;
        Styles.Add(new FluentTheme());
    }
}
