using System;
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
            var gtkHost = new Uno.UI.Runtime.Skia.Gtk.GtkHost(() => new App());
            gtkHost.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GTK Host error: {ex.Message}");
        }
    }
}
