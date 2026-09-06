using System.Globalization;
using System.Windows.Data;

namespace DivinityModManager.Converters;

public sealed class InverseBooleanConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is bool enabled && !enabled;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is bool disabled && !disabled;
}
