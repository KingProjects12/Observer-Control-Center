using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Observer.ControlCenter;

public partial class MiniIslandWindow : Window
{
    private readonly MainWindow _console;
    private string? _updateVersion;
    private bool _switching;
    private bool _isAnimating;

    public MiniIslandWindow(MainWindow console)
    {
        InitializeComponent();
        _console = console;
        Loaded += (_, _) => PositionAtTopCenter();
    }

    private void PositionAtTopCenter()
    {
        // SystemParameters.WorkArea respects the Windows taskbar.
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 7;
    }

    public async Task ShowAnimatedAsync()
    {
        _isAnimating = true;
        try
        {
            PositionAtTopCenter();
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            IslandOffset.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
            IslandOffset.Y = -14;
            if (!IsVisible) Show();
            await Task.WhenAll(WindowMotion.FadeAsync(this, 1, 210),
                WindowMotion.MoveYAsync(IslandOffset, 0, 250));
        }
        finally { _isAnimating = false; }
    }

    public async Task HideAnimatedAsync()
    {
        if (!IsVisible) return;
        _isAnimating = true;
        try
        {
            await Task.WhenAll(WindowMotion.FadeAsync(this, 0, 135),
                WindowMotion.MoveYAsync(IslandOffset, -10, 135));
            Hide();
        }
        finally { _isAnimating = false; }
    }

    public void SetSummary(int devices, string text)
    {
        if (_updateVersion != null) return;
        var label = text.TrimStart('●', ' ');
        StatusText.Text = devices > 0 ? $"{label} · {devices} устройств" : label;
        CountText.Text = "ОТКРЫТЬ";
    }

    public void SetUpdateAvailable(string version)
    {
        _updateVersion = version;
        StatusText.Text = "ДОСТУПНА НОВАЯ ВЕРСИЯ";
        CountText.Text = version;
    }

    public void ClearUpdateNotice()
    {
        _updateVersion = null;
        StatusText.Text = "Локальный просмотр";
        CountText.Text = "ОТКРЫТЬ";
    }

    private async void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_switching || e.ChangedButton != MouseButton.Left) return;
        _switching = true;
        try
        {
            bool openUpdateCenter = _updateVersion != null;
            await _console.RestoreFromIslandAsync();
            if (openUpdateCenter) _console.ShowUpdateCenter();
        }
        finally { _switching = false; }
    }

    private void IslandSurface_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_switching || _isAnimating) return;
        IslandOffset.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(-2, TimeSpan.FromMilliseconds(150))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new DoubleAnimation(0.88, TimeSpan.FromMilliseconds(170)));
    }

    private void IslandSurface_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_switching || _isAnimating) return;
        IslandOffset.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(170))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new DoubleAnimation(0.64, TimeSpan.FromMilliseconds(170)));
    }
}
