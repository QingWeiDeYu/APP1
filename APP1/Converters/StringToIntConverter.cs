using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace SmartAgri.Converters;


#if false
public sealed class StringToIntConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && int.TryParse(str, out int result))
            return result;
        return 0; // 或者 return null，根据你的需求
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue.ToString();
        return string.Empty; // 或者 return null，根据你的需求
    }
}
#endif


public sealed class StringToIntConverter : IValueConverter
{
    // 绑定源(int) -> 目标(string)
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
            return i.ToString(culture);

        if (value is string s)
            return s; // 容错：若本就是 string

        return string.Empty;
    }

    // 绑定目标(string) -> 源(int)
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, NumberStyles.Integer, culture, out var i))
            return i;

        return 0;
    }
}

