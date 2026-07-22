using System;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnoAotIntegrationApp;

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
            Text = "Uno AOT Integration Application",
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

        window.Content = stackPanel;
        window.Activate();
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        int port = 9234;

        // Start Uno Platform CDP diagnostics server
        WinUI.Diagnostics.Cdp.CdpServer.EnsureInitialized();
        WinUI.Diagnostics.Cdp.CdpServer.Start(port);
        Console.WriteLine($"Uno AOT Application listening on CDP port: {port}");

        try
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
                Text = "Uno AOT Integration Application",
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

            window.Content = stackPanel;
            WinUI.Diagnostics.Cdp.CdpServer.GetOrCreateTarget(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Headless fallback: {ex.Message}");
        }

        Console.WriteLine("Uno AOT App main loop active. Keeping process alive.");
        while (true)
        {
            Thread.Sleep(100);
        }
    }
}
