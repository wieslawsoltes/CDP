using Avalonia.Media;

namespace CdpInspectorApp.ViewModels;

public class PinnedExpressionViewModel : ViewModelBase
{
    private string _expression = "";
    private string _result = "";
    private bool _isError;

    public string Expression
    {
        get => _expression;
        set => RaiseAndSetIfChanged(ref _expression, value);
    }

    public string Result
    {
        get => _result;
        set
        {
            if (RaiseAndSetIfChanged(ref _result, value))
            {
                OnPropertyChanged(nameof(ResultBrush));
            }
        }
    }

    public bool IsError
    {
        get => _isError;
        set
        {
            if (RaiseAndSetIfChanged(ref _isError, value))
            {
                OnPropertyChanged(nameof(ResultBrush));
            }
        }
    }

    public IBrush ResultBrush => IsError ? Brushes.Red : Brushes.LightGreen;
}
