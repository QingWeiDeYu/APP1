using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace SmartAgri.Converters;

public class BoolToTextConverter : IValueConverter
{
    // ConverterParameter 形如 "已连|连接"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = parameter as string ?? "已连|连接";
        var parts = s.Split('|', 2, StringSplitOptions.None);
        var trueText = parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) ? parts[0] : "已连";
        var falseText = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : "连接";

        var isTrue = value is true; // 支持 null/非 bool 时按 false 处理
        return isTrue ? trueText : falseText;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}