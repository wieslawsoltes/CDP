namespace Avalonia.Diagnostics.Cdp.Rdp;

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using CDP.Rdp.Session;

public partial class RdpView : UserControl
{
    public static readonly StyledProperty<string> HostProperty =
        AvaloniaProperty.Register<RdpView, string>(nameof(Host), "127.0.0.1");

    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<RdpView, int>(nameof(Port), 3389);

    public static readonly StyledProperty<string> UsernameProperty =
        AvaloniaProperty.Register<RdpView, string>(nameof(Username), string.Empty);

    public static readonly StyledProperty<string> PasswordProperty =
        AvaloniaProperty.Register<RdpView, string>(nameof(Password), string.Empty);

    public static readonly StyledProperty<string> DomainProperty =
        AvaloniaProperty.Register<RdpView, string>(nameof(Domain), string.Empty);

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<RdpView, bool>(nameof(IsConnected), false);

    public static readonly StyledProperty<ICommand?> ConnectCommandProperty =
        AvaloniaProperty.Register<RdpView, ICommand?>(nameof(ConnectCommand));

    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty =
        AvaloniaProperty.Register<RdpView, ICommand?>(nameof(DisconnectCommand));

    public static readonly StyledProperty<IRdpSession?> SessionProperty =
        AvaloniaProperty.Register<RdpView, IRdpSession?>(nameof(Session));

    public string Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public int Port
    {
        get => GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }

    public string Username
    {
        get => GetValue(UsernameProperty);
        set => SetValue(UsernameProperty, value);
    }

    public string Password
    {
        get => GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public string Domain
    {
        get => GetValue(DomainProperty);
        set => SetValue(DomainProperty, value);
    }

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public ICommand? ConnectCommand
    {
        get => GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public ICommand? DisconnectCommand
    {
        get => GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    public IRdpSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public RdpView()
    {
        InitializeComponent();
    }
}
