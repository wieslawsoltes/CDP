namespace Xaml.Compiler.TypeSystem;

public interface IXamlProperty
{
    string Name { get; }
    IXamlType DeclaringType { get; }
    IXamlType PropertyType { get; }
    bool IsAttached { get; }
    bool IsReadOnly { get; }
    bool IsContent { get; }
    
    object? GetValue(object target);
    void SetValue(object target, object? value);
    object? ConvertValue(string stringValue);
}
