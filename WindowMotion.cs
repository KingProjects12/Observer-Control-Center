using System.Windows;
using System.Windows.Media.Animation;

namespace Observer.ControlCenter;

/// <summary>
/// Small, UI-thread-only, non-blocking visual transitions. The final value is committed
/// after the animation so Show/Hide cycles do not leave a stale animation clock behind.
/// </summary>
internal static class WindowMotion
{
    public static Task FadeAsync(Window window, double target, int milliseconds = 180) =>
        AnimateAsync(window, UIElement.OpacityProperty, target, milliseconds);

    public static Task MoveYAsync(System.Windows.Media.TranslateTransform transform, double target,
        int milliseconds = 200) =>
        AnimateAsync(transform, System.Windows.Media.TranslateTransform.YProperty, target, milliseconds);

    private static Task AnimateAsync(IAnimatable target, DependencyProperty property, double value, int ms)
    {
        // Clearing a previous animation avoids the WPF animation clock retaining its value.
        target.BeginAnimation(property, null);
        if (ms <= 0)
        {
            ((DependencyObject)target).SetValue(property, value);
            return Task.CompletedTask;
        }
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            To = value,
            Duration = TimeSpan.FromMilliseconds(ms),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        EventHandler? onFinished = null;
        onFinished = (_, _) =>
        {
            animation.Completed -= onFinished;
            target.BeginAnimation(property, null);
            ((DependencyObject)target).SetValue(property, value);
            completed.TrySetResult(true);
        };
        animation.Completed += onFinished;
        target.BeginAnimation(property, animation);
        return completed.Task;
    }
}
