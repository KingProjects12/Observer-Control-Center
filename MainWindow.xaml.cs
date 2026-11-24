using System.Collections.ObjectModel;
using System.Data;
using System.Net.Sockets;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Observer.ControlCenter.Models;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TrafficDevice> _devices = [];
    private readonly ObservableCollection<TopBar> _bars = [];
    private MiniIslandWindow? _island;
    private DataTable? _connections;
    private string _page = "overview";
    private string _reportPath = "";
    private bool _purple;
    private bool _windowTransitionBusy;

    public MainWindow()
    {
        InitializeComponent();
        TrafficGrid.ItemsSource = _devices;
        TopBars.ItemsSource = _bars;
        AddLog("OBSERVER 2.0.4 PREVIEW  //  WPF / .NET 10 / GitHub release notices");
        AddLog("READ-ONLY  //  this preview does not connect to Keenetic API or store credentials");
        ShowPage("overview");
        StartUpdateChecks();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var file = ReportLocator.FindLatest("08_Top_Traffic.csv");
        if (file != null) LoadTraffic(file);
        else AddLog("Нет локальных 08_Top_Traffic.csv. Нажми ЗАГРУЗИТЬ CSV.");
        await WindowMotion.FadeAsync(this, 1, 280);
    }

    private void AddLog(string message)
    {
        LogList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (LogList.Items.Count > 200) LogList.Items.RemoveAt(0);
        if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        BottomStatus.Text = message;
    }

    private void LoadTraffic(string file)
    {
        try
        {
            var data = CsvTableReader.ReadTraffic(file);
            _devices.Clear();
            foreach (var record in data) _devices.Add(record);
            _reportPath = file;
            SearchBox.Clear();
            var bytes = data.Sum(x => (decimal)x.TotalBytes);
            KpiDevices.Text = data.Count.ToString(CultureInfo.InvariantCulture);
            KpiTraffic.Text = FormatBytes(bytes);
            KpiActive.Text = data.Count(x => x.TotalBytes > 0).ToString(CultureInfo.InvariantCulture);
            var max = data.Count == 0 ? 1L : Math.Max(1, data.Max(x => x.TotalBytes));
            _bars.Clear();
            foreach (var device in data.Take(10))
            {
                _bars.Add(new TopBar
                {
                    Name = device.Name,
                    Value = device.Total,
                    Width = Math.Clamp(260.0 * device.TotalBytes / max, 2, 260)
                });
            }
            PageSubtitle.Text = Path.GetFileName(Path.GetDirectoryName(file)) + "  •  " + (data.FirstOrDefault()?.Window ?? "неизвестное окно");
            _island?.SetSummary(data.Count, "●  Локальная статистика загружена");
            AddLog($"CSV IMPORT  //  {data.Count} устройств; {FormatBytes(bytes)}; источник={Path.GetFileName(file)}");
            ShowPage("traffic");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(ex.Message, "Импорт TOP TRAFFIC", MessageBoxButton.OK, MessageBoxImage.Warning);
            AddLog("CSV ERROR  //  " + ex.GetType().Name);
        }
    }

    private static string FormatBytes(decimal b)
    {
        const decimal MiB = 1024 * 1024, GiB = MiB * 1024;
        return b >= GiB ? (b / GiB).ToString("0.00", CultureInfo.InvariantCulture) + " GiB"
            : (b / MiB).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
    }

    private void ShowPage(string page)
    {
        _page = page;
        bool showConnections = page == "connections";
        TrafficGrid.Visibility = showConnections ? Visibility.Collapsed : Visibility.Visible;
        ConnectionsGrid.Visibility = showConnections ? Visibility.Visible : Visibility.Collapsed;
        if (page == "connections")
        {
            PageTitle.Text = "СОЕДИНЕНИЯ / WHO → WHERE";
            TableTitle.Text = "НАБЛЮДАЕМЫЕ СОЕДИНЕНИЯ";
            TableHint.Text = _connections == null ? "Загрузи 01_All_Connections.csv из v1.9.1" : $"Строк: {_connections.Rows.Count}";
        }
        else
        {
            PageTitle.Text = page == "traffic" ? "TOP TRAFFIC / NETWORK ANALYTICS" : "ОБЗОР СЕТИ";
            TableTitle.Text = "ТОП ПОТРЕБИТЕЛЕЙ";
            TableHint.Text = "Объём за выбранное окно монитора Keenetic";
        }
    }

    private void Overview_Click(object sender, RoutedEventArgs e) => ShowPage("overview");
    private void Traffic_Click(object sender, RoutedEventArgs e) => ShowPage("traffic");
    private void Connections_Click(object sender, RoutedEventArgs e)
    {
        if (_connections == null)
        {
            string? path = ReportLocator.FindLatest("01_All_Connections.csv");
            if (path != null) LoadConnections(path);
            else AddLog("Нет 01_All_Connections.csv. Импортируй файл вручную.");
        }
        ShowPage("connections");
    }

    private void LoadConnections(string file)
    {
        try
        {
            _connections = CsvTableReader.ReadTable(file);
            ConnectionsGrid.ItemsSource = _connections.DefaultView;
            AddLog($"CONNECTIONS  //  {_connections.Rows.Count} строк; {Path.GetFileName(file)}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            AddLog("CONNECTIONS ERROR  //  " + ex.Message);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Title = "Импортировать отчёт Keenetic Observer",
            Filter = "CSV (*.csv)|*.csv|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(ReportLocator.ResultsRoot) ? ReportLocator.ResultsRoot
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (fileDialog.ShowDialog(this) != true) return;
        if (Path.GetFileName(fileDialog.FileName).StartsWith("01_All_Connections", StringComparison.OrdinalIgnoreCase))
        {
            LoadConnections(fileDialog.FileName);
            ShowPage("connections");
        }
        else LoadTraffic(fileDialog.FileName);
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(ReportLocator.ResultsRoot))
        {
            AddLog("REPORTS  //  папка отчётов ещё не создана");
            return;
        }
        var folder = _reportPath.Length > 0 ? Path.GetDirectoryName(_reportPath) : ReportLocator.ResultsRoot;
        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            Arguments = $"\"{folder}\"", UseShellExecute = true
        });
        AddLog("REPORTS  //  открыта папка с локальными данными");
    }

    private async void RouterCheck_Click(object sender, RoutedEventArgs e)
    {
        var address = RouterIP.Text.Trim();
        AddLog("HEALTH  //  проверка ровно двух TCP-портов управляемого адреса: 80 / 443");
        try
        {
            string result = await RouterHealthProbe.CheckAsync(address);
            AddLog("HEALTH  //  " + result + "  (это не проверка авторизации API)");
        }
        catch (Exception ex) when (ex is ArgumentException or SocketException)
        {
            AddLog("HEALTH ERROR  //  " + ex.Message);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TrafficGrid?.ItemsSource == null) return;
        string phrase = SearchBox.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(TrafficGrid.ItemsSource);
        view.Filter = x => x is TrafficDevice r && (phrase.Length == 0 ||
            r.Name.Contains(phrase, StringComparison.OrdinalIgnoreCase) ||
            r.IP.Contains(phrase, StringComparison.OrdinalIgnoreCase) ||
            r.MAC.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        _purple = !_purple;
        var accent = (Color)ColorConverter.ConvertFromString(_purple ? "#B392FF" : "#3DE7EA")!;
        Application.Current.Resources["Accent"] = new SolidColorBrush(accent);
        AddLog("THEME  //  акцент: " + (_purple ? "lavender" : "cyan"));
    }

    // Exactly one visible surface at a time: the full console OR its compact island.
    private async void Island_Click(object sender, RoutedEventArgs e)
    {
        if (_windowTransitionBusy || !IsVisible) return;
        _windowTransitionBusy = true;
        BtnIsland.IsEnabled = false;
        try
        {
            if (_island == null)
            {
                _island = new MiniIslandWindow(this);
                _island.Closed += (_, _) => _island = null;
            }
            RefreshUpdateBanner();
            _island.SetSummary(_devices.Count, "●  Локальный просмотр");
            await WindowMotion.FadeAsync(this, 0, 140);
            Hide();
            await _island.ShowAnimatedAsync();
        }
        finally
        {
            BtnIsland.IsEnabled = true;
            _windowTransitionBusy = false;
        }
    }

    // Always restore to a maximized console. The island must disappear first;
    // otherwise its Topmost window remains visible over the application.
    internal async Task RestoreFromIslandAsync()
    {
        if (_windowTransitionBusy) return;
        _windowTransitionBusy = true;
        try
        {
            if (_island is { IsVisible: true })
                await _island.HideAnimatedAsync();

            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Show();
            WindowState = WindowState.Maximized;
            Activate();
            await WindowMotion.FadeAsync(this, 1, 185);
        }
        finally
        {
            _windowTransitionBusy = false;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount >= 2) { ToggleMaximize(); return; }
        try { DragMove(); } catch (InvalidOperationException) { }
    }
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosed(EventArgs e)
    {
        _updateShutdown.Cancel();
        _updateTimer.Stop();
        _updateNotice?.Close();
        _updateShutdown.Dispose();
        _island?.Close();
        base.OnClosed(e);
    }
}
