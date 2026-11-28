using Observer.ControlCenter.Services;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Observer.ControlCenter;

public partial class MiniIslandWindow : Window
{
    private readonly MainWindow _console;
    private string? _updateVersion;
    private bool _switching;

    public MiniIslandWindow(MainWindow console)
    {
        InitializeComponent();
        _console = console;
        SetSummary(0,UiText.T("Ready","Готово"));
        Loaded += (_, _) => PositionAtTopCenter();
    }

    private void PositionAtTopCenter()
    {
        var owner = new WindowInteropHelper(_console).Handle;
        var island = new WindowInteropHelper(this).EnsureHandle();
        var area = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (owner == IntPtr.Zero || !GetMonitorInfo(MonitorFromWindow(owner, 2), ref area))
        {
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
            Top = SystemParameters.WorkArea.Top + 8;
            return;
        }
        double scale = GetDpiForWindow(owner) / 96d;
        int x = (area.Work.Left + area.Work.Right - (int)Math.Round(Width * scale)) / 2;
        int y = area.Work.Top + (int)Math.Round(8 * scale);
        SetWindowPos(island, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public int Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    public async Task ShowAnimatedAsync()
    {
        PositionAtTopCenter();
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        IslandOffset.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        IslandOffset.Y = -6;
        if (!IsVisible) Show();
        await Task.WhenAll(WindowMotion.FadeAsync(this, 1, 160),
            WindowMotion.MoveYAsync(IslandOffset, 0, 160));
    }

    public async Task HideAnimatedAsync()
    {
        if (!IsVisible) return;
        await Task.WhenAll(WindowMotion.FadeAsync(this, 0, 135),
            WindowMotion.MoveYAsync(IslandOffset, -6, 135));
        Hide();
    }

    private string _operation = "";
    private string _detail = "";
    private bool _busy;
    public void SetSummary(int devices, string text)
    {
        if (_operation.Length == 0) SetOperation(text.TrimStart('●',' '), devices > 0 ? devices.ToString() : "", false);
    }

    internal void SetOperation(string operation, string detail, bool busy)
    {
        _operation = operation; _detail = detail; _busy = busy;
        if (_updateVersion != null && !busy) { SetUpdateAvailable(_updateVersion); return; }
        StatusText.Text = operation.Length == 0 ? UiText.T("Ready","Готово") : operation;
        CountText.Text = detail;
        CountText.SetResourceReference(ForegroundProperty, busy ? "Accent" :
            detail == UiText.T("Complete","Завершено") || detail == "COMPLETE" ? "Positive" : "TextMain");
    }

    public void SetUpdateAvailable(string version)
    {
        _updateVersion = version;
        if (_busy) return;
        StatusText.Text = UiText.T("NEW VERSION AVAILABLE", "ДОСТУПНА НОВАЯ ВЕРСИЯ");
        CountText.Text = version;
        CountText.SetResourceReference(ForegroundProperty,"Accent");
    }

    public void ClearUpdateNotice()
    {
        _updateVersion = null;
        SetOperation(_operation,_detail,_busy);
    }

    private async void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_switching || e.ChangedButton != MouseButton.Left) return;
        _switching = true;
        try
        {
            bool openUpdateCenter = _updateVersion != null && !_busy;
            await _console.RestoreFromIslandAsync();
            if (openUpdateCenter) _console.ShowUpdateCenter();
        }
        finally { _switching = false; }
    }

}
