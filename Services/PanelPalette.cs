using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Observer.ControlCenter.Services;
internal static class PanelPalette
{
    // Convert legacy local brush references to dynamic resources so a live theme switch keeps state.
    internal static void Bind(DependencyObject root)
    {
        var keys=new[]{"DialogBg","DialogText","DialogAccent","DialogField","DialogFieldStroke","DialogLog","Accent","TextSecondary"};
        void Walk(DependencyObject node)
        {
            if(node is FrameworkElement e)
            {
                var props=new List<DependencyProperty>();
                if(e is Control){props.Add(Control.BackgroundProperty);props.Add(Control.ForegroundProperty);props.Add(Control.BorderBrushProperty);}
                if(e is TextBlock)props.Add(TextBlock.ForegroundProperty);
                foreach(var prop in props)
                {
                    var current=e.ReadLocalValue(prop);
                    if(current is not Brush)continue;
                    foreach(var key in keys)if(ReferenceEquals(current,Application.Current.TryFindResource(key))){e.SetResourceReference(prop,key);break;}
                }
            }
            foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())Walk(child);
        }
        Walk(root);
    }
}
