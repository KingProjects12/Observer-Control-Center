using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public sealed class KeeneticCollectorWindow : Window
{
    private readonly TextBox _address, _username, _target, _samples, _interval;
    private readonly PasswordBox _password;
    private readonly CheckBox _https;
    private readonly Button _collect, _cancel;
    private readonly TextBlock _status;
    private CancellationTokenSource? _running;
    public NatReport? Result { get; private set; }

    private static string T(string en, string ru) => UiText.T(en, ru);
    public KeeneticCollectorWindow(string address)
    {
        Title = "Observer · Keenetic NAT";
        Width = 620; Height = 660; MinWidth = 520; MinHeight = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)FindResource("DialogBg");
        Foreground = (Brush)FindResource("DialogText"); FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(22) };
        var scroll = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = scroll;
        root.Children.Add(new TextBlock { Text = T("KEENETIC / LIVE NAT", "KEENETIC / ЖИВОЙ NAT"), FontSize = 21, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("DialogAccent"), Margin = new Thickness(0,0,0,14) });
        TextBox Field(string name, string value)
        {
            root.Children.Add(new TextBlock { Text = name, Margin = new Thickness(0,5,0,4) });
            var box = new TextBox { Text = value, Style = (Style)FindResource("TextInput"), Margin = new Thickness(0,0,0,5) };
            root.Children.Add(box); return box;
        }
        _address = Field(T("Router IPv4 (optional :port)", "IPv4 роутера (можно :порт)"), address);
        _username = Field(T("Username", "Логин"), "");
        root.Children.Add(new TextBlock { Text = T("Password (not saved)", "Пароль (не сохраняется)"), Margin = new Thickness(0,5,0,4) });
        _password = new PasswordBox { Background = (Brush)FindResource("DialogField"), Foreground = (Brush)FindResource("DialogText"), Padding = new Thickness(10,8,10,8), Margin = new Thickness(0,0,0,5) };
        root.Children.Add(_password);
        _target = Field(T("Target network (IPv4 CIDR)", "Целевая сеть (IPv4 CIDR)"), "");
        _samples = Field(T("Snapshots (1–12)", "Снимки (1–12)"), "3");
        _interval = Field(T("Interval in seconds (3–60)", "Интервал в секундах (3–60)"), "5");
        _https = new CheckBox { Content = T("Use HTTPS (recommended)", "Использовать HTTPS (рекомендуется)"), IsChecked = true, Foreground = (Brush)FindResource("DialogText"), Margin = new Thickness(0,7,0,7) };
        root.Children.Add(_https);
        root.Children.Add(new TextBlock { Text = T("Authorized networks only. Read-only GET requests; POST /auth for login only. NAT may miss VPN/LAN flows. No router settings are changed.", "Только разрешённые сети. GET только для чтения, POST /auth только для входа. NAT может не видеть VPN/LAN. Настройки роутера не изменяются."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
        var buttons = new WrapPanel();
        _collect = new Button { Content = T("CONNECT AND COLLECT NAT", "ПОДКЛЮЧИТЬСЯ И СОБРАТЬ NAT"), Style = (Style)FindResource("AccentButton"), Margin = new Thickness(0,0,8,8) };
        _cancel = new Button { Content = T("CANCEL", "ОТМЕНА"), Style = (Style)FindResource("FlatButton"), IsEnabled = false };
        buttons.Children.Add(_collect); buttons.Children.Add(_cancel); root.Children.Add(buttons);
        _status = new TextBlock { Text = T("Ready", "Готово к сбору"), TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("DialogAccent") };
        root.Children.Add(_status);
        _collect.Click += Collect;
        _cancel.Click += (_, _) => _running?.Cancel();
    }

    private async void Collect(object sender, RoutedEventArgs e)
    {
        if (_running != null) return;
        bool https = _https.IsChecked == true;
        if (!https && MessageBox.Show(this, T("HTTP is not encrypted. Use only on a trusted, authorized network or VPN. Continue?", "HTTP не шифруется. Используйте только в доверенной разрешённой сети или VPN. Продолжить?"), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (!int.TryParse(_samples.Text, out int samples) || !int.TryParse(_interval.Text, out int interval))
        { _status.Text = T("Invalid sampling settings.", "Некорректные параметры сбора."); return; }
        using var cts = new CancellationTokenSource();
        _running = cts;
        _collect.IsEnabled = false; _cancel.IsEnabled = true;
        string password = _password.Password;
        _password.Clear();
        try
        {
            var progress = new Progress<string>(s => _status.Text = s);
            Result = await KeeneticNatCollector.CollectAsync(_address.Text, https, _username.Text, password, _target.Text, samples, interval, ReportLocator.ResultsRoot, progress, cts.Token);
            DialogResult = true;
        }
        catch (OperationCanceledException) { _status.Text = T("Cancelled or timed out. No complete report published.", "Отменено или превышено время ожидания. Полный отчёт не сформирован."); }
        catch (Exception ex) { _status.Text = T("Collection failed: ", "Ошибка сбора: ") + ex.Message; }
        finally { password = ""; _running = null; _collect.IsEnabled = true; _cancel.IsEnabled = false; }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_running != null && Result == null) { _running.Cancel(); e.Cancel = true; }
        _password.Clear();
        base.OnClosing(e);
    }
}
