using System;

namespace UnoSampleApp;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        var host = new Uno.UI.Runtime.Skia.Gtk.GtkHost(() => new App());
        host.Run();
    }
}
