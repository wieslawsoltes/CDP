using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CdpInspectorApp.Models;

public class ClassItemModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    private string _name = "";
    private bool _isEnabled;
    private readonly Action<ClassItemModel> _onChanged;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
                _onChanged?.Invoke(this);
            }
        }
    }

    public ClassItemModel(string name, bool isEnabled, Action<ClassItemModel> onChanged)
    {
        Name = name;
        IsEnabled = isEnabled;
        _onChanged = onChanged;
    }
}
