using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace WindowsOptimizer_App.Converters;

public sealed class SelectionConverter : IValueConverter
{
    private static Brush? _accentBrush;

    public static Brush GetBrush(bool isSelected)
    {
        if (isSelected)
        {
            if (_accentBrush == null)
            {
                // Un azul verdadero, brillante y destacado (DodgerBlue)
                _accentBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 30, 144, 255)); // #1E90FF
            }
            return _accentBrush;
        }
        
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return GetBrush(b);
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
