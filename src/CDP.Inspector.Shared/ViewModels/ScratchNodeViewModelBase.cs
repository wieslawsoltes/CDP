#nullable enable

using System;
using System.Text.Json.Nodes;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;

namespace CdpInspectorApp.ViewModels;

public abstract class ScratchNodeViewModelBase : NodeViewModel, IDisposable
{
    private string? _linkedElementId;
    private string? _linkedElementName;

    public string? LinkedElementId
    {
        get => _linkedElementId;
        set
        {
            if (RaiseAndSetIfChanged(ref _linkedElementId, value))
            {
                OnPropertyChanged(nameof(IsLinked));
            }
        }
    }

    public string? LinkedElementName
    {
        get => _linkedElementName;
        set => RaiseAndSetIfChanged(ref _linkedElementName, value);
    }

    public bool IsLinked => !string.IsNullOrEmpty(LinkedElementId);

    public virtual ICommand? LinkSelectedNodeCommand { get; protected set; }
    public virtual ICommand? ShowInTreeCommand { get; protected set; }

    public virtual string OutputJson => "";
    public virtual JsonNode? OutputJsonNode => null;

    protected ScratchNodeViewModelBase()
    {
        Content = this;
    }

    protected void AddInputPin(string id, string name)
    {
        Inputs.Add(new PinViewModel
        {
            Id = id,
            Name = name,
            Kind = PinKind.Input,
            Owner = this,
            Index = Inputs.Count
        });
    }

    protected void AddOutputPin(string id, string name)
    {
        Outputs.Add(new PinViewModel
        {
            Id = id,
            Name = name,
            Kind = PinKind.Output,
            Owner = this,
            Index = Outputs.Count
        });
    }

    public virtual void Dispose()
    {
    }
}

public interface IImportExportNode
{
    Func<System.Threading.Tasks.Task<string?>>? PayloadImportHandler { get; set; }
    Func<System.Threading.Tasks.Task>? PayloadExportHandler { get; set; }
    string RawJsonData { get; set; }
}
