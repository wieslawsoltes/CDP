using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xaml.Compiler.Adapters;

public interface IUiFrameworkAdapter
{
    IReadOnlyCollection<string> XamlFileExtensions { get; }
    string DefaultXmlNamespace { get; }
    bool IsControl(object target);
    object? GetParent(object control);
    IReadOnlyCollection<object> GetChildren(object parent);
    string GetTypeName(object control);
    string? GetClassFullName(object control);
    object? GetPropertyValue(object control, string propertyName);
    Task ApplyAttributeLiveAsync(object control, string propertyName, string valueString);
    Task RemoveAttributeLiveAsync(object control, string propertyName);
    Task RemoveNodeLiveAsync(object control);
    Task<object> InstantiateXamlFragmentAsync(string xamlFragment, Dictionary<string, string> inheritedNamespaces);
    Task<bool> ReplaceChildLiveAsync(object oldChild, object newChild);
}
