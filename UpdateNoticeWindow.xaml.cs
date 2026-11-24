using System.Windows;
using System.Windows.Threading;

namespace Observer.ControlCenter;

/// <summary>
/// Lightweight, app-owned desktop notice like Atoll's island toast; not a Windows push service.
/// The app must be running. No update binary is downloaded or executed.
/// </summary>
public partial class UpdateNoticeWindow : Window
{
    private readonly Action _openDetails;
    private readonly DispatcherTimer _autoClose = new() { Interval = TimeSpan.FromSeconds(14) };

    public UpdateNoticeWindow(string version, Action openDetails)
    {
        InitializeComponent();
        VersionText.Text = $"Доступна новая версия {version}";
        _openDetails = openDetails;
        Loaded += async (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 14;
            Top = area.Bottom - Height - 14;
            if (SystemParameters.ClientAreaAnimation)
            {
                Opacity = 0;
                await WindowMotion.FadeAsync(this, 1, 230);
            }
            _autoClose.Start();
        };
        _autoClose.Tick += (_, _) => Close();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _openDetails();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _autoClose.Stop();
        base.OnClosed(e);
    }
}
