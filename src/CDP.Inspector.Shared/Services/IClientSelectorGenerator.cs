using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public interface IClientSelectorGenerator
{
    string GenerateSelector(DomNodeModel node);
}
