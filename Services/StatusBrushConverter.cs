using System.Globalization;
using System.Windows.Data;
using Observer.ControlCenter.Models;
namespace Observer.ControlCenter.Services;
internal sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {string s=value?.ToString()??"";return new ConsoleEntry(s,ConsoleEntry.Classify(s)).Color;}
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
