using System;
using Microsoft.UI.Xaml;
using Uno.UI.Runtime.Skia.Gtk;

namespace UnoSampleApp;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        var host = new GtkHost(() => new App());
        host.Run();
    }
}
