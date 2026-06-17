using Avalonia.Media;

namespace CdpInspectorApp.Models;

public class ConsoleItemModel
{
    public string Expression { get; }
    public string Result { get; }
    public bool IsError { get; }
    public IBrush ResultBrush => IsError ? Brushes.Red : Brushes.LightGreen;

    public ConsoleItemModel(string expr, string res, bool isError = false)
    {
        Expression = expr;
        Result = res;
        IsError = isError;
    }
}
