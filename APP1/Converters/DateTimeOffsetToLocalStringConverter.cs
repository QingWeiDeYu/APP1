using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace SmartAgri.Converters;

public class DateTimeOffsetToLocalStringConverter : IValueConverter
{
    // ConverterParameter Àý£º"yyyy-MM-dd HH:mm"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto) return string.Empty;

        var fmtParam = parameter as string;
        var format = string.IsNullOrWhiteSpace(fmtParam) ? "yyyy-MM-dd HH:mm" : fmtParam!;
        return dto.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}