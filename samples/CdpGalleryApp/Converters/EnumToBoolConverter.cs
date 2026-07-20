using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CdpGalleryApp.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
            
        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return parameter;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
