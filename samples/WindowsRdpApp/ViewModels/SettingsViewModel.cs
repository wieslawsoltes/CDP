using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using ReactiveUI;

namespace WindowsRdpApp.ViewModels;

public class SettingsViewModel : ReactiveObject
{
    private string _selectedTheme = "Dark";
    private int _cdpPort = 9225;
    private bool _enableDoubleBuffering = true;
    private int _fpsCap = 60;
    private bool _showDirtyRectangles = false;
    private bool _autoReconnect = true;
    private string _statusText = "Settings active";

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTheme, value);
            ApplyTheme(value);
        }
    }

    public int CdpPort
    {
        get => _cdpPort;
        set => this.RaiseAndSetIfChanged(ref _cdpPort, value);
    }

    public bool EnableDoubleBuffering
    {
        get => _enableDoubleBuffering;
        set => this.RaiseAndSetIfChanged(ref _enableDoubleBuffering, value);
    }

    public int FpsCap
    {
        get => _fpsCap;
        set => this.RaiseAndSetIfChanged(ref _fpsCap, value);
    }

    public bool ShowDirtyRectangles
    {
        get => _showDirtyRectangles;
        set => this.RaiseAndSetIfChanged(ref _showDirtyRectangles, value);
    }

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => this.RaiseAndSetIfChanged(ref _autoReconnect, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ICommand SwitchThemeDarkCommand { get; }
    public ICommand SwitchThemeLightCommand { get; }

    public SettingsViewModel()
    {
        SwitchThemeDarkCommand = ReactiveCommand.Create(() => SelectedTheme = "Dark");
        SwitchThemeLightCommand = ReactiveCommand.Create(() => SelectedTheme = "Light");
    }

    public void ApplyTheme(string themeName)
    {
        StatusText = $"Theme updated to {themeName}";

        if (Application.Current != null)
        {
            if (themeName.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
        }
    }
}
