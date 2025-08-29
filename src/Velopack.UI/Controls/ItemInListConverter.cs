using System.Collections;

namespace Velopack.UI.MultiSelectTreeView.Controls;

public class ItemInListConverter : System.Windows.Data.IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values == null)
        {
            return false;
        }
        if (values.Length == 2 && values[0] is IList list)
        {
            return list.Contains(values[1]);
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
