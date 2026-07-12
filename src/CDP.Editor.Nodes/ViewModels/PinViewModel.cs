#nullable enable

using System;

namespace CDP.Editor.Nodes.ViewModels;

public enum PinKind
{
    Input,
    Output
}

public class PinViewModel : NodeEditorViewModelBase
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "";
    private PinKind _kind;
    private NodeViewModel? _owner;
    private int _index;

    public string Id
    {
        get => _id;
        set => RaiseAndSetIfChanged(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public PinKind Kind
    {
        get => _kind;
        set => RaiseAndSetIfChanged(ref _kind, value);
    }

    public NodeViewModel? Owner
    {
        get => _owner;
        set => RaiseAndSetIfChanged(ref _owner, value);
    }

    public int Index
    {
        get => _index;
        set
        {
            if (RaiseAndSetIfChanged(ref _index, value))
            {
                OnPropertyChanged(nameof(Top));
            }
        }
    }

    public double Top
    {
        get
        {
            if (Owner == null) return 0;
            int count = Kind == PinKind.Input ? Owner.Inputs.Count : Owner.Outputs.Count;
            return (Index + 1) * Owner.Height / (count + 1) - 5.0;
        }
    }

    public void RaiseTopChanged()
    {
        OnPropertyChanged(nameof(Top));
    }
}
