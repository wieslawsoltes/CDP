using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Avalonia.Diagnostics.Cdp.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Avalonia.Diagnostics.Cdp.Tests;

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
#if !AVALONIA_V11
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Diagnostics.Cdp.Tests/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml")
        });
#endif
    }
}
