using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeeneticObserver;
using Microsoft.Win32;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

// WPF shell for the complete original v1.9.1 collector. No legacy WinForms UI.
internal sealed class LegacyCollectorWindow : Window
{
    private readonly TextBox _address, _user, _target, _samples, _interval, _log;
    private readonly PasswordBox _password;
    private readonly CheckBox _https, _vpn;
    private readonly ComboBox _period, _view;
    private readonly TextBlock _status;
    private readonly List<Button> _commands = new();
    private readonly Button _stop, _show, _excel, _reports;
    private CancellationTokenSource? _running;
    private string? _folder, _excelPath;
    public string? SelectedCsv { get; private set; }
    public string? ReportFolder => _folder;
    public string Address => _address.Text.Trim();
    private static string T(string en, string ru) => UiText.T(en, ru);
    private static readonly (string File, string En, string Ru)[] Views =
    {
        ("07_Office_To_Connections.csv", "To office", "В офис"),
        ("05_Office_Related.csv", "Office related", "Все связанные с офисом"),
        ("01_All_Connections.csv", "All connections", "Все соединения"),
        ("02_Target_Connections.csv", "Target connections", "Целевые соединения"),
        ("06_Office_Related_Links.csv", "Office links", "Направления офиса"),
        ("03_Source_To_Destination.csv", "Source to destination", "Источник → назначение"),
        ("04_Clients.csv", "Clients", "Клиенты"),
        ("02_Other_Connections.csv", "Other connections", "Прочие соединения"),
        ("08_Top_Traffic.csv", "TOP Traffic", "ТОП трафика"),
        ("09_Traffic_Comparison.csv", "Traffic comparison", "Сравнение трафика"),
        ("10_Traffic_Flags.csv", "Traffic flags", "Признаки для проверки")
    };
    public LegacyCollectorWindow(string address, bool vpn = false, bool traffic = false)
    {
        Title = T("Observer · Network collector", "Observer · Сбор данных");
        Width = 760; Height = 820; MinWidth = 570; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(7,9,12)); Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = T("NETWORK COLLECTION / READ-ONLY", "СБОР СЕТЕВЫХ ДАННЫХ / ТОЛЬКО ЧТЕНИЕ"), FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brushes.Turquoise, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        void Label(string s) => root.Children.Add(new TextBlock { Text = s, Margin = new Thickness(0,6,0,4), TextWrapping = TextWrapping.Wrap });
        TextBox Field(string s, string v) { Label(s); var b = new TextBox { Text = v, Style = (Style)FindResource("TextInput") }; root.Children.Add(b); return b; }
        _address = Field(T("Keenetic IPv4 (optional :port)", "IPv4 Keenetic (можно :порт)"), address);
        _user = Field(T("Username", "Логин"), "");
        Label(T("Password (not saved)", "Пароль (не сохраняется)"));
        _password = new PasswordBox { Background = new SolidColorBrush(Color.FromRgb(12,15,18)), Foreground = Brushes.White, Padding = new Thickness(10), BorderBrush = Brushes.DarkSlateGray };
        root.Children.Add(_password);
        _https = new CheckBox { Content = "HTTPS", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0,10,0,4) }; root.Children.Add(_https);
        _vpn = new CheckBox { Content = T("VPN REMOTE — existing authorized VPN route", "VPN REMOTE — существующий разрешённый VPN-маршрут"), IsChecked = vpn, Foreground = Brushes.White, Margin = new Thickness(0,4,0,6) }; root.Children.Add(_vpn);
        _target = Field(T("Office destination /24 (saved locally)", "Сеть назначения офиса /24 (сохраняется локально)"), LoadTarget());
        _samples = Field(T("NAT snapshots (1–12)", "Снимки NAT (1–12)"), "3");
        _interval = Field(T("Interval in seconds (1–60)", "Интервал в секундах (1–60)"), "5");
        Label(T("TOP Traffic window", "Период TOP Traffic"));
        _period = new ComboBox { ItemsSource = new[] { T("3 minutes", "3 минуты"), T("1 hour", "1 час"), T("3 hours", "3 часа"), T("24 hours", "24 часа") }, SelectedIndex = traffic ? 3 : 0 }; root.Children.Add(_period);
        var buttons = new WrapPanel { Margin = new Thickness(0,12,0,4) }; root.Children.Add(buttons);
        Button Button(string label, RoutedEventHandler handler, bool accent = false)
        {
            var b = new Button { Content = label, Style = (Style)FindResource(accent ? "AccentButton" : "FlatButton"), Margin = new Thickness(0,0,8,8) }; b.Click += handler; buttons.Children.Add(b); return b;
        }
        _commands.Add(Button(T("COLLECT NAT", "СОБРАТЬ NAT"), async (_,_) => await Run("nat"), true));
        _commands.Add(Button(T("TOP TRAFFIC", "ТОП ТРАФИКА"), async (_,_) => await Run("traffic"), true));
        _commands.Add(Button(T("VPN / CHECK TCP", "VPN / ПРОВЕРИТЬ TCP"), async (_,_) => await Run("probe")));
        _commands.Add(Button(T("TOOLS ▾", "ИНСТРУМЕНТЫ ▾"), (sender,_) => {
            var menu = new ContextMenu { PlacementTarget=(UIElement)sender, Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom };
            var importItem = new MenuItem { Header=T("Import central flow CSV", "Импорт CSV центрального шлюза") }; importItem.Click += async (_,_) => await Run("import"); menu.Items.Add(importItem);
            var demoItem = new MenuItem { Header=T("Offline demo test", "Офлайн демо-тест") }; demoItem.Click += async (_,_) => await Run("demo"); menu.Items.Add(demoItem);
            menu.IsOpen=true;
        }));
        _stop = Button(T("STOP", "СТОП"), (_,_) => _running?.Cancel()); _stop.IsEnabled = false;
        Label(T("Report view", "Отображение отчёта"));
        _view = new ComboBox { DisplayMemberPath = "Label" }; root.Children.Add(_view);
        _view.SelectionChanged += (_,_) => { if (_show != null) _show.IsEnabled = _running == null && _view.SelectedItem != null; };
        var output = new WrapPanel { Margin = new Thickness(0,10,0,4) }; root.Children.Add(output); buttons = output;
        _show = Button(T("SHOW IN OBSERVER", "ПОКАЗАТЬ В OBSERVER"), (_,_) => { if (_view.SelectedItem is View v && _folder != null) { SelectedCsv = Path.Combine(_folder,v.File); DialogResult = true; } }); _show.IsEnabled = false;
        _excel = Button(T("OPEN EXCEL", "ОТКРЫТЬ EXCEL"), (_,_) => Open(_excelPath)); _excel.IsEnabled = false;
        _reports = Button(T("OPEN REPORTS", "ОТКРЫТЬ ОТЧЁТЫ"), (_,_) => Open(_folder)); _reports.IsEnabled = false;
        _status = new TextBlock { Text = T("Ready. Choose NAT for destinations; TOP Traffic for volume.", "Готово. NAT — адреса назначения; TOP Traffic — объём трафика."), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Turquoise }; root.Children.Add(_status);
        _log = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Black, Foreground = Brushes.White, Margin = new Thickness(0,8,0,0) }; root.Children.Add(_log);
        Label(T("Read-only. The existing VPN must already be connected. NAT and TOP Traffic do not prove all VPN/LAN flows. Raw reports are confidential.", "Только чтение. VPN должен уже существовать и быть подключён. NAT и TOP Traffic не показывают гарантированно все VPN/LAN-потоки. Сырые отчёты конфиденциальны."));
    }
    private sealed record View(string File, string Label);
    private static string TargetPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ObserverControlCenter", "collector-target.txt");
    private static string LoadTarget() { try { return File.Exists(TargetPath) ? File.ReadAllText(TargetPath).Trim() : ""; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; } }
    private static void SaveTarget(string value) { try { Directory.CreateDirectory(Path.GetDirectoryName(TargetPath)!); File.WriteAllText(TargetPath, value); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    private void Open(string? path)
    {
        try { if (!string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path))) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
    private void Report(string message) { _log.AppendText(message + Environment.NewLine); _log.ScrollToEnd(); _status.Text = message; }
    private void SetReport(string folder, string? excel, string status, bool traffic)
    {
        _folder = folder; _excelPath = excel;
        var views = Views.Where(v=>File.Exists(Path.Combine(folder,v.File))).Select(v=>new View(v.File,T(v.En,v.Ru))).ToList();
        _view.ItemsSource = views;
        _view.SelectedItem = traffic ? views.FirstOrDefault(v=>v.File=="08_Top_Traffic.csv") : views.FirstOrDefault();
        Report(status);
    }
    private async Task Run(string operation)
    {
        if (_running != null) return;
        var addr = Address; var user = _user.Text.Trim(); bool https = _https.IsChecked==true, vpn = _vpn.IsChecked==true;
        int samples = 3, interval = 5, detail = _period.SelectedIndex;
        string? import = null;
        try
        {
            if (operation is "nat" or "traffic" or "probe")
            {
                var uri = Engine.ParseRouterUri(addr, https);
                if (vpn || operation == "probe") RemoteVpn.RequirePrivateTarget(uri);
                if (operation != "probe" && (user.Length == 0 || _password.Password.Length == 0)) throw new ArgumentException(T("Enter username and password.", "Введите логин и пароль."));
                if (operation != "probe" && !https && MessageBox.Show(this,T("HTTP is not encrypted. Continue only on an authorized trusted network or VPN?", "HTTP не шифруется. Продолжить только в разрешённой доверенной сети или VPN?"),Title,MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
            }
            if (operation == "demo") Engine.SetTargetSubnet(Engine.DemoSubnet);
            if (operation is "nat" or "import") { Engine.SetTargetSubnet(_target.Text); SaveTarget(Engine.TargetSubnet); }
            if (operation == "nat" && (!int.TryParse(_samples.Text,out samples) || samples is <1 or >12 || !int.TryParse(_interval.Text,out interval) || interval is <1 or >60)) throw new ArgumentException(T("Invalid sample settings.", "Некорректные параметры снимков."));
            if (operation == "import") { var dialog = new OpenFileDialog { Filter="CSV (*.csv)|*.csv", CheckFileExists=true }; if (dialog.ShowDialog(this)!=true) return; import=dialog.FileName; }
        }
        catch(Exception ex) { Report(ex.Message); return; }
        using var cts = new CancellationTokenSource(); _running=cts; _stop.IsEnabled=true;
        foreach(var b in _commands) b.IsEnabled=false;
        _show.IsEnabled=_excel.IsEnabled=_reports.IsEnabled=false;
        string secret = _password.Password; _password.Clear();
        var progress = new Progress<string>(Report); Action<string> log = s => ((IProgress<string>)progress).Report(s);
        try
        {
            if(operation == "probe")
            {
                var uri=Engine.ParseRouterUri(addr,https);
                var probe=await Task.Run(()=>RemoteVpn.ProbeTcp(uri,6000),cts.Token);
                RemoteVpn.WriteProbeDiagnostic(uri,probe);
                Report(probe.Reachable ? $"TCP REACHABLE {probe.ElapsedMilliseconds} ms; local={probe.LocalAddress}; adapter={probe.NetworkAdapter}. VPN route not verified." : "TCP FAILED: "+probe.Error);
            }
            else if(operation=="traffic")
            {
                var result=await Task.Run(()=>TrafficMonitor.Run(addr,user,secret,https,vpn,detail,cts.Token,log),cts.Token);
                SetReport(result.Folder,result.Excel,T("Traffic devices: ","Устройств в рейтинге: ")+result.Rows.Count,true);
            }
            else
            {
                CollectionResult result = operation=="import" ? await Task.Run(()=>CentralFlowImporter.Import(import!,log,cts.Token),cts.Token) : await Task.Run(()=>Engine.Run(operation=="demo",addr,user,secret,https,samples,interval,cts.Token,log,vpn),cts.Token);
                SetReport(result.Folder,result.Excel,$"{result.Status} | ALL {result.All.Count} | TO OFFICE {result.Target.Count} | RELATED {result.OfficeRelated.Count}",false);
            }
        }
        catch(OperationCanceledException){Report(T("Cancelled.","Отменено."));}
        catch(Exception ex){Report(T("Operation failed: ","Ошибка операции: ")+ex.Message);}
        finally {secret="";_running=null; _stop.IsEnabled=false;foreach(var b in _commands)b.IsEnabled=true;_show.IsEnabled=_view.SelectedItem!=null;_excel.IsEnabled=File.Exists(_excelPath);_reports.IsEnabled=Directory.Exists(_folder);}
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if(_running!=null){_running.Cancel();e.Cancel=true;} _password.Clear();base.OnClosing(e);
    }
}
