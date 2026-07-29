namespace CDP.Rdp.Tests.Applications;

using System.Reflection;
using Xunit;

public sealed class WindowsRdpAppProgramTests
{
    [Theory]
    [InlineData("70000")]
    [InlineData("0")]
    [InlineData("invalid")]
    public void ParsePort_InvalidValueUsesDisplayedAndBoundDefault(string value)
    {
        Assert.Equal(9225, InvokeParsePort(["--port", value]));
    }

    [Fact]
    public void ParsePort_ValidValueIsPreserved()
    {
        Assert.Equal(9333, InvokeParsePort(["--port", "9333"]));
    }

    private static int InvokeParsePort(string[] args)
    {
        Type programType = typeof(WindowsRdpApp.App).Assembly.GetType("WindowsRdpApp.Program")!;
        MethodInfo parsePort = programType.GetMethod(
            "ParsePort",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (int)parsePort.Invoke(null, [args])!;
    }
}
