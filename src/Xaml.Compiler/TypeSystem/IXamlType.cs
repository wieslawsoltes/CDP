using System.Collections.Generic;

namespace Xaml.Compiler.TypeSystem;

public interface IXamlType
{
    string Name { get; }
    IXamlNamespace Namespace { get; }
    IXamlType? BaseType { get; }
    bool IsMarkupExtension { get; }
    bool IsCollection { get; }
    IXamlProperty? ContentProperty { get; }
    
    IReadOnlyCollection<IXamlProperty> GetProperties();
    IXamlProperty? GetProperty(string name);
    object? CreateInstance(object[]? args = null);
}
