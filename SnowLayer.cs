using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Observer.ControlCenter;

// Decorative header only. No input handling, no background task, no network dependency.
public sealed class SnowLayer : Canvas
{
    private bool _requested;
    private readonly List<TranslateTransform> _motions=new();
    public SnowLayer()
    {
        IsHitTestVisible=false;ClipToBounds=true;
        Loaded+=(_,_)=>Rebuild();Unloaded+=(_,_)=>ClearSnow();
        IsVisibleChanged+=(_,_)=>Rebuild();
        SizeChanged+=(_,_)=>Rebuild();
    }
    internal void SetSnowRequested(bool value){if(_requested==value)return;_requested=value;Rebuild();}
    private void ClearSnow()
    {
        foreach(var motion in _motions)motion.BeginAnimation(TranslateTransform.YProperty,null);
        _motions.Clear();Children.Clear();
    }
    private void Rebuild()
    {
        ClearSnow();
        if(!_requested||!IsLoaded||!IsVisible||!SystemParameters.ClientAreaAnimation||ActualWidth<20||ActualHeight<20)return;
        var rng=new Random(812);
        int count=Math.Clamp((int)(ActualWidth/42),12,32);
        for(int i=0;i<count;i++)
        {
            double size=1.8+rng.NextDouble()*2.8;
            var dot=new Ellipse{Width=size,Height=size,Fill=Brushes.White,Opacity=.24+rng.NextDouble()*.35,IsHitTestVisible=false};
            SetLeft(dot,rng.NextDouble()*Math.Max(1,ActualWidth-size));SetTop(dot,0);
            var motion=new TranslateTransform();dot.RenderTransform=motion;Children.Add(dot);_motions.Add(motion);
            var duration=TimeSpan.FromSeconds(5+rng.NextDouble()*7);
            var animation=new DoubleAnimation(-size,ActualHeight+size,new Duration(duration)){RepeatBehavior=RepeatBehavior.Forever};
            var clock=animation.CreateClock();
            motion.ApplyAnimationClock(TranslateTransform.YProperty,clock);
            clock.Controller?.SeekAlignedToLastTick(TimeSpan.FromTicks((long)(duration.Ticks*rng.NextDouble())),TimeSeekOrigin.BeginTime);
        }
    }
}
