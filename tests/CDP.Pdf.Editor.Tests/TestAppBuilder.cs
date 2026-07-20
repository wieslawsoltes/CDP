using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CDP.Pdf.Editor.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace CDP.Pdf.Editor.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
