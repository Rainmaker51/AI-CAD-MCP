using System;
using System.Globalization;
using System.Windows.Data;

namespace Rainmaker.Axis;
internal sealed class AnchorLabelConverter:IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>ColumnOptions.AnchorLabel(value as string??"");
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
