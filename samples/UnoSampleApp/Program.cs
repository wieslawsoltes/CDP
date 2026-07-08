using System;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnoSampleApp;

public class App : Application
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        
        var stackPanel = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(20)
        };

        var textBlock = new TextBlock
        {
            Name = "txtTitle",
            Text = "Uno CDP Sample Application",
            FontSize = 24
        };
        stackPanel.Children.Add(textBlock);

        var button = new Button
        {
            Name = "btnClickMe",
            Content = "Click Me!"
        };
        button.Click += (s, e) =>
        {
            textBlock.Text = "Button Clicked!";
        };
        stackPanel.Children.Add(button);

        var inputField = new TextBox
        {
            Name = "txtInput",
            PlaceholderText = "Type something here..."
        };
        stackPanel.Children.Add(inputField);

        window.Content = stackPanel;
        window.Activate();

        Console.WriteLine("Uno window created and activated.");
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        int port = 9225;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out port);
            }
        }

        // Start Uno Platform CDP diagnostics server
        WinUI.Diagnostics.Cdp.CdpServer.EnsureInitialized();
        WinUI.Diagnostics.Cdp.CdpServer.Start(port);
        Console.WriteLine($"Uno Application listening on CDP port: {port}");

        try
        {
            Application.Start(_ => new App());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Running application headless due to: {ex.Message}");
            
            // Headless mock setup for automated testing
            var window = new Window();
            var stackPanel = new StackPanel
            {
                Spacing = 10,
                Padding = new Thickness(20)
            };

            var textBlock = new TextBlock { Name = "txtTitle", Text = "Headless Uno App" };
            var button = new Button { Name = "btnClickMe", Content = "Click Me" };
            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(button);
            window.Content = stackPanel;
            
            // Register window with server
            WinUI.Diagnostics.Cdp.CdpServer.GetOrCreateTarget(window);

            Console.WriteLine("Headless target registered. Press Ctrl+C to exit.");
            while (true)
            {
                Thread.Sleep(100);
            }
        }
    }
}
