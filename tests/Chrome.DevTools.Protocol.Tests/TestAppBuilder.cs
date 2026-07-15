using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using System;

[assembly: AvaloniaTestApplication(typeof(Chrome.DevTools.Protocol.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Chrome.DevTools.Protocol.Tests;

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
        Styles.Add(new FluentTheme());
    }
}
