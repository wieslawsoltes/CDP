using System;
using System.ComponentModel;
using System.Reflection;
using Xaml.Compiler.TypeSystem;

namespace Xaml.Compiler.TypeSystemImpl;

public class XamlProperty : IXamlProperty
{
    private readonly PropertyInfo? _propertyInfo;
    private readonly MethodInfo? _attachedGetMethod;
    private readonly MethodInfo? _attachedSetMethod;

    public string Name { get; }
    public IXamlType DeclaringType { get; }
    public IXamlType PropertyType { get; }
    public bool IsAttached { get; }
    public bool IsReadOnly { get; }
    public bool IsContent { get; }

    // Standard Property Constructor
    public XamlProperty(string name, IXamlType declaringType, IXamlType propertyType, PropertyInfo propertyInfo, bool isContent)
    {
        Name = name;
        DeclaringType = declaringType;
        PropertyType = propertyType;
        _propertyInfo = propertyInfo;
        IsAttached = false;
        IsReadOnly = !propertyInfo.CanWrite;
        IsContent = isContent;
    }

    // Attached Property Constructor
    public XamlProperty(string name, IXamlType declaringType, IXamlType propertyType, MethodInfo? getMethod, MethodInfo? setMethod)
    {
        Name = name;
        DeclaringType = declaringType;
        PropertyType = propertyType;
        _attachedGetMethod = getMethod;
        _attachedSetMethod = setMethod;
        IsAttached = true;
        IsReadOnly = setMethod == null;
        IsContent = false;
    }

    public object? GetValue(object target)
    {
        if (IsAttached)
        {
            if (_attachedGetMethod == null)
                throw new InvalidOperationException($"Attached property {Name} does not have a static getter.");
            return _attachedGetMethod.Invoke(null, new[] { target });
        }
        return _propertyInfo!.GetValue(target);
    }

    public void SetValue(object target, object? value)
    {
        if (IsReadOnly)
            throw new InvalidOperationException($"Property {Name} is read-only.");

        if (IsAttached)
        {
            _attachedSetMethod!.Invoke(null, new[] { target, value });
        }
        else
        {
            _propertyInfo!.SetValue(target, value);
        }
    }

    public object? ConvertValue(string stringValue)
    {
        var targetType = ((XamlType)PropertyType).UnderlyingType;
        if (targetType == typeof(string)) return stringValue;

        // Fast-path primitives for speed and trimming compatibility
        if (targetType == typeof(int)) return int.Parse(stringValue);
        if (targetType == typeof(double)) return double.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(float)) return float.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(bool)) return bool.Parse(stringValue);
        if (targetType.IsEnum) return Enum.Parse(targetType, stringValue, true);

        // Standard TypeConverter
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(stringValue);
        }

        // Check attributes for custom converter mapping
        var converterAttr = targetType.GetCustomAttribute<TypeConverterAttribute>()
            ?? _propertyInfo?.GetCustomAttribute<TypeConverterAttribute>();
        if (converterAttr != null)
        {
            Type? converterType = Type.GetType(converterAttr.ConverterTypeName);
            if (converterType == null)
            {
                var typeNameOnly = converterAttr.ConverterTypeName.Split(',')[0].Trim();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    converterType = asm.GetType(typeNameOnly) ?? asm.GetType(converterAttr.ConverterTypeName);
                    if (converterType != null) break;
                }
            }
            if (converterType != null)
            {
                var customConverter = Activator.CreateInstance(converterType) as TypeConverter;
                if (customConverter != null && customConverter.CanConvertFrom(typeof(string)))
                {
                    return customConverter.ConvertFromInvariantString(stringValue);
                }
            }
        }

        throw new InvalidOperationException($"Unable to convert '{stringValue}' to type {targetType.FullName}");
    }
}
