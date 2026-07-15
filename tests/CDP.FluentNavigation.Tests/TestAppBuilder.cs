using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using System;

[assembly: AvaloniaTestApplication(typeof(CDP.FluentNavigation.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace CDP.FluentNavigation.Tests;

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
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://CDP.FluentNavigation.Tests/"))
        {
            Source = new Uri("avares://CDP.FluentNavigation/Themes/Generic.axaml")
        });
    }
}
