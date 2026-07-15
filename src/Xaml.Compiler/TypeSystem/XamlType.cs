using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xaml.Compiler.TypeSystem;

namespace Xaml.Compiler.TypeSystemImpl;

public class XamlType : IXamlType
{
    private readonly Lazy<IReadOnlyCollection<IXamlProperty>> _properties;
    private readonly Lazy<IXamlProperty?> _contentProperty;

    public string Name { get; }
    public IXamlNamespace Namespace { get; }
    public IXamlType? BaseType { get; }
    public Type UnderlyingType { get; }
    public bool IsMarkupExtension { get; }
    public bool IsCollection { get; }
    public IXamlProperty? ContentProperty => _contentProperty.Value;

    public XamlType(
        string name,
        IXamlNamespace @namespace,
        IXamlType? baseType,
        Type underlyingType,
        Func<IXamlType, IReadOnlyCollection<IXamlProperty>> propertiesFactory)
    {
        Name = name;
        Namespace = @namespace;
        BaseType = baseType;
        UnderlyingType = underlyingType;

        IsMarkupExtension = underlyingType.Name.EndsWith("Extension") || 
                            underlyingType.GetInterfaces().Any(i => i.Name == "IMarkupExtension");
        
        IsCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType) && 
                       underlyingType != typeof(string);

        _properties = new Lazy<IReadOnlyCollection<IXamlProperty>>(() => propertiesFactory(this));
        
        _contentProperty = new Lazy<IXamlProperty?>(() =>
        {
            var contentAttr = underlyingType.GetCustomAttributes(true)
                .FirstOrDefault(a => a.GetType().Name == "ContentPropertyAttribute");
            if (contentAttr != null)
            {
                var propName = contentAttr.GetType().GetProperty("Name")?.GetValue(contentAttr) as string;
                if (!string.IsNullOrEmpty(propName))
                {
                    return GetProperty(propName);
                }
            }
            return BaseType?.ContentProperty;
        });
    }

    public IReadOnlyCollection<IXamlProperty> GetProperties() => _properties.Value;

    public IXamlProperty? GetProperty(string name)
    {
        var prop = _properties.Value.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (prop == null && BaseType != null)
        {
            return BaseType.GetProperty(name);
        }
        return prop;
    }

    public object? CreateInstance(object[]? args = null)
    {
        return Activator.CreateInstance(UnderlyingType, args);
    }
}
