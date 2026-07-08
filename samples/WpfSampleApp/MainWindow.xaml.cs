using System.Windows;

namespace WpfSampleApp;

public partial class MainWindow : Window
{
    private int _clickCount = 0;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnClickMe_Click(object sender, RoutedEventArgs e)
    {
        _clickCount++;
        lblStatus.Text = $"Clicked {_clickCount} time(s)! Input: '{txtInput.Text}'";
    }
}
