using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer_App.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FindingStatus status)
        {
            return status switch
            {
                FindingStatus.AlreadyOptimal or FindingStatus.Applied => new SolidColorBrush(Microsoft.UI.Colors.Green),
                FindingStatus.Drifted => new SolidColorBrush(Microsoft.UI.Colors.Red),
                _ => new SolidColorBrush(Microsoft.UI.Colors.Orange)
            };
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
