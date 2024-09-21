using System.Windows.Data;

namespace System.Windows.Controls;

public class ThicknessLeftConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, Globalization.CultureInfo culture) => value switch
    {
        int => new Thickness { Left = (int)value },
        double => new Thickness { Left = (double)value },
        _ => new Thickness()
    };

    public object ConvertBack(object value, Type targetType, object parameter, Globalization.CultureInfo culture) => throw new NotImplementedException();
}
