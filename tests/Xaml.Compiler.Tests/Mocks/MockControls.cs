using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;

namespace Xaml.Compiler.Tests.Mocks;

[AttributeUsage(AttributeTargets.Class)]
public class ContentPropertyAttribute : Attribute
{
    public string Name { get; }
    public ContentPropertyAttribute(string name)
    {
        Name = name;
    }
}

public class MockControl
{
    public string? Name { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public MockThickness Margin { get; set; } = new(0);

    [TypeConverter(typeof(MockColorConverter))]
    public MockColor? Background { get; set; }
}

[ContentProperty("Content")]
public class MockButton : MockControl
{
    public object? Content { get; set; }
    public bool IsPressed { get; set; }
}

public class MockGrid : MockControl
{
    private static readonly ConcurrentDictionary<object, int> _rowValues = new();
    private static readonly ConcurrentDictionary<object, int> _columnValues = new();

    public static int GetRow(object target)
    {
        return _rowValues.TryGetValue(target, out var val) ? val : 0;
    }

    public static void SetRow(object target, int value)
    {
        _rowValues[target] = value;
    }

    public static int GetColumn(object target)
    {
        return _columnValues.TryGetValue(target, out var val) ? val : 0;
    }

    public static void SetColumn(object target, int value)
    {
        _columnValues[target] = value;
    }
}

[TypeConverter(typeof(MockThicknessConverter))]
public class MockThickness
{
    public double Left { get; }
    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }

    public MockThickness(double uniform) : this(uniform, uniform, uniform, uniform) { }

    public MockThickness(double left, double top, double right, double bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

public class MockThicknessConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            var parts = str.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                double uniform = double.Parse(parts[0], CultureInfo.InvariantCulture);
                return new MockThickness(uniform);
            }
            if (parts.Length == 4)
            {
                double l = double.Parse(parts[0], CultureInfo.InvariantCulture);
                double t = double.Parse(parts[1], CultureInfo.InvariantCulture);
                double r = double.Parse(parts[2], CultureInfo.InvariantCulture);
                double b = double.Parse(parts[3], CultureInfo.InvariantCulture);
                return new MockThickness(l, t, r, b);
            }
        }
        return base.ConvertFrom(context, culture, value);
    }
}

public class MockColor
{
    public string Hex { get; }
    public MockColor(string hex) => Hex = hex;
}

public class MockColorConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            return new MockColor(str);
        }
        return base.ConvertFrom(context, culture, value);
    }
}

public class MockMarkupExtension
{
    public string? ProvideValue() => "Value";
}

