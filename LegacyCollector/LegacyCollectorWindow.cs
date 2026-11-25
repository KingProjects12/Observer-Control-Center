using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Data;
using Observer.ControlCenter.Models;
using KeeneticObserver;
using Microsoft.Win32;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

// WPF shell for the complete original v1.9.1 collector. No legacy WinForms UI.
internal sealed class LegacyCollectorWindow : Window
{
    private readonly TextBox _address, _user, _target, _samples, _interval;
    private readonly RichTextBox _log;
    private readonly TextBlock _batchSummary;
    private readonly ProgressBar _batchProgress;
    private readonly DataGrid _batchGrid;
    private readonly Button _retry,_access;
    private readonly Dictionary<string,RouterLive> _live=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,(string User,string Password)> _overrides=new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Text.StringBuilder _logText=new();
    private BatchNatResult? _lastBatch;
    private sealed class RouterLive { public string Router{get;set;}="";public string Status{get;set;}="NOT_STARTED";public string Samples{get;set;}="";public string Office{get;set;}=""; }
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
    public string Address => BatchNatCollector.Split(_address.Text).FirstOrDefault() ?? "";
    private static string T(string en, string ru) => UiText.T(en, ru);
    private static readonly (string File, string En, string Ru)[] Views =
    {
        ("07_Office_To_Connections.csv", "To office", "В офис"),
        ("00_Router_Status.csv", "Router status", "Статус роутеров"),
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
        Width = 810; Height = 880; MinWidth = 600; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DarkTitleBar.Apply(this);
        Background = new SolidColorBrush(Color.FromRgb(7,9,12)); Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = T("NETWORK COLLECTION / READ-ONLY", "СБОР СЕТЕВЫХ ДАННЫХ / ТОЛЬКО ЧТЕНИЕ"), FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brushes.Turquoise, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        StackPanel section = root;
        void Group(string en, string ru, string hintEn, string hintRu)
        {
            var card = new Border { Style=(Style)FindResource("CardBorder"), Margin=new Thickness(0,0,0,12) };
            section = new StackPanel(); card.Child=section; root.Children.Add(card);
            section.Children.Add(new TextBlock { Text=T(en,ru), Foreground=(Brush)FindResource("Accent"), FontSize=13, FontWeight=FontWeights.SemiBold });
            section.Children.Add(new TextBlock { Text=T(hintEn,hintRu), Foreground=(Brush)FindResource("TextSecondary"), FontSize=11, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,4,0,8) });
        }
        void Label(string s) => section.Children.Add(new TextBlock { Text = s, Margin = new Thickness(0,6,0,4), TextWrapping = TextWrapping.Wrap });
        TextBox Field(string s, string v) { Label(s); var b = new TextBox { Text = v, Style = (Style)FindResource("TextInput") }; section.Children.Add(b); return b; }
        Group("01 / CONNECTION", "01 / ПОДКЛЮЧЕНИЕ", "Enter the managed router and credentials. The password is never saved.", "Введите адрес роутера и данные входа. Пароль не сохраняется.");
        _address = Field(T("Keenetic IPv4 list — one per line (optional :port)", "IP роутеров Keenetic — по одному на строку (можно :порт)"), address);
        _address.AcceptsReturn=true; _address.MinHeight=88; _address.MaxHeight=150; _address.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Label(T("NAT accepts multiple routers with the same login and password. Up to three collect in parallel; one combined office report.", "NAT принимает несколько роутеров с общими логином и паролем. До трёх одновременно; один общий отчёт по офису."));
        _user = Field(T("Username", "Логин"), "");
        Label(T("Password (not saved)", "Пароль (не сохраняется)"));
        _password = new PasswordBox { Background = new SolidColorBrush(Color.FromRgb(12,15,18)), Foreground = Brushes.White, Padding = new Thickness(10), BorderBrush = Brushes.DarkSlateGray };
        section.Children.Add(_password);
        _https = new CheckBox { Content = "HTTPS", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0,10,0,4) }; section.Children.Add(_https);
        _vpn = new CheckBox { Content = T("VPN REMOTE — existing authorized VPN route", "VPN REMOTE — существующий разрешённый VPN-маршрут"), IsChecked = vpn, Foreground = Brushes.White, Margin = new Thickness(0,4,0,6) }; section.Children.Add(_vpn);
        Group("02 / COLLECTION SETTINGS", "02 / ПАРАМЕТРЫ СБОРА", "NAT finds destinations; TOP Traffic measures volume for a selected window.", "NAT показывает назначения; TOP Traffic — объём за выбранный период.");
        _target = Field(T("Office destination /24 (saved locally)", "Сеть назначения офиса /24 (сохраняется локально)"), LoadTarget());
        _samples = Field(T("NAT snapshots (1–12)", "Снимки NAT (1–12)"), "3");
        _interval = Field(T("Interval in seconds (1–60)", "Интервал в секундах (1–60)"), "5");
        Label(T("TOP Traffic window", "Период TOP Traffic"));
        _period = new ComboBox { ItemsSource = new[] { T("3 minutes", "3 минуты"), T("1 hour", "1 час"), T("3 hours", "3 часа"), T("24 hours", "24 часа") }, SelectedIndex = traffic ? 3 : 0 }; section.Children.Add(_period);
        Group("03 / ACTIONS", "03 / ДЕЙСТВИЯ", "Choose a collection or test the existing VPN route. Extra actions are under Tools.", "Выберите сбор или проверку существующего VPN-маршрута. Дополнительные действия — в инструментах.");
        var buttons = new WrapPanel { Margin = new Thickness(0,12,0,4) }; section.Children.Add(buttons);
        Button Button(string label, RoutedEventHandler handler, bool accent = false, string? hint = null)
        {
            var b = new Button { Content = label, Style = (Style)FindResource(accent ? "AccentButton" : "FlatButton"), Margin = new Thickness(0,0,8,8), ToolTip=hint }; b.Click += handler; buttons.Children.Add(b); return b;
        }
        _commands.Add(Button(T("COLLECT NAT", "СОБРАТЬ NAT"), async (_,_) => await Run("nat"), true, T("Read active connections and match office destinations.", "Собрать активные соединения и найти назначения в офис.")));
        _commands.Add(Button(T("TOP TRAFFIC", "ТОП ТРАФИКА"), async (_,_) => await Run("traffic"), true, T("Read the selected traffic monitor window.", "Собрать трафик за выбранный период.")));
        _commands.Add(Button(T("VPN / CHECK TCP", "VPN / ПРОВЕРИТЬ TCP"), async (_,_) => await Run("probe"), false, T("Test TCP reachability using the existing route; does not create a VPN.", "Проверить доступность TCP по существующему маршруту; VPN не создаётся.")));
        _commands.Add(Button(T("TOOLS ▾", "ИНСТРУМЕНТЫ ▾"), (sender,_) => {
            var menu = new ContextMenu { PlacementTarget=(UIElement)sender, Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom };
            var importItem = new MenuItem { Header=T("Import central flow CSV", "Импорт CSV центрального шлюза") }; importItem.Click += async (_,_) => await Run("import"); menu.Items.Add(importItem);
            var demoItem = new MenuItem { Header=T("Offline demo test", "Офлайн демо-тест") }; demoItem.Click += async (_,_) => await Run("demo"); menu.Items.Add(demoItem);
            menu.IsOpen=true;
        }));
        _commands.Add(Button(T("ROUTER LOGIN", "ЛОГИН РОУТЕРА"), (_,_)=>EditLogin()));
        _retry=Button(T("RETRY INCOMPLETE", "ПОВТОРИТЬ ПРОБЛЕМНЫЕ"), async (_,_)=>await Run("retry"));_retry.IsEnabled=false;
        _stop = Button(T("STOP", "СТОП"), (_,_) => _running?.Cancel()); _stop.IsEnabled = false; _stop.ToolTip=T("Cancel collection and retain available results.","Остановить сбор, сохранить доступные результаты.");
        _batchSummary=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Turquoise,Margin=new Thickness(0,8,0,4)};section.Children.Add(_batchSummary);
        _batchProgress=new ProgressBar{Minimum=0,Maximum=1,Height=9,Margin=new Thickness(0,3,0,8)};section.Children.Add(_batchProgress);
        _batchGrid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,MaxHeight=210,MinHeight=75};
        foreach(string key in new[]{"Router","Status","Samples","Office"}){var c=new DataGridTextColumn{Header=key,Binding=new Binding(key),Width=new DataGridLength(1,DataGridLengthUnitType.Star)};if(key=="Status"){var st=new Style(typeof(TextBlock));st.Setters.Add(new Setter(TextBlock.ForegroundProperty,new Binding(key){Converter=new StatusBrushConverter()}));c.ElementStyle=st;}_batchGrid.Columns.Add(c);}section.Children.Add(_batchGrid);
        Group("04 / RESULTS", "04 / РЕЗУЛЬТАТЫ", "Select a report after collection, then view it or open Excel.", "После сбора выберите отчёт и откройте его в Observer или Excel.");
        Label(T("Report view", "Отображение отчёта"));
        _view = new ComboBox { DisplayMemberPath = "Label" }; section.Children.Add(_view);
        _view.SelectionChanged += (_,_) => { if (_show != null) _show.IsEnabled = _running == null && _view.SelectedItem != null; };
        var output = new WrapPanel { Margin = new Thickness(0,10,0,4) }; section.Children.Add(output); buttons = output;
        _show = Button(T("SHOW IN OBSERVER", "ПОКАЗАТЬ В OBSERVER"), (_,_) => { if (_view.SelectedItem is View v && _folder != null) { SelectedCsv = Path.Combine(_folder,v.File); DialogResult = true; } }); _show.IsEnabled = false; _show.ToolTip=T("Open the selected CSV in the main table.","Показать выбранный CSV в главной таблице.");
        _excel = Button(T("OPEN EXCEL", "ОТКРЫТЬ EXCEL"), (_,_) => Open(_excelPath)); _excel.IsEnabled = false; _excel.ToolTip=T("Open the generated Excel workbook.","Открыть сформированный файл Excel.");
        _reports = Button(T("OPEN REPORTS", "ОТКРЫТЬ ОТЧЁТЫ"), (_,_) => Open(_folder)); _reports.IsEnabled = false; _reports.ToolTip=T("Open the current report folder.","Открыть папку текущего отчёта.");
        _access=Button(T("ANALYZE ACCESS", "АНАЛИЗ ДОСТУПОВ"),(_,_)=>{var csv=_folder==null?null:Path.Combine(_folder,"07_Office_To_Connections.csv");new AccessAnalyzerWindow(csv,_target.Text){Owner=this}.ShowDialog();});_access.IsEnabled=false;
        Group("05 / ACTIVITY", "05 / ЖУРНАЛ", "Progress and diagnostic details. Copy the log when troubleshooting.", "Прогресс и диагностика. При ошибке скопируйте журнал.");
        _status = new TextBlock { Text = T("Ready. Choose NAT for destinations; TOP Traffic for volume.", "Готово. NAT — адреса назначения; TOP Traffic — объём трафика."), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Turquoise }; section.Children.Add(_status);
        _log = new RichTextBox { IsReadOnly = true, MinHeight = 140, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Black, Foreground = Brushes.White, Margin = new Thickness(0,8,0,0) }; section.Children.Add(_log);
        var copyLog = new Button { Content=T("COPY LOG","КОПИРОВАТЬ ЖУРНАЛ"), Style=(Style)FindResource("FlatButton"), HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,8,0,0) };
        copyLog.Click += (_,_) => { if(_logText.Length==0)return; try { Clipboard.SetText(_logText.ToString()); } catch(System.Runtime.InteropServices.COMException) { _status.Text=T("Clipboard unavailable.","Буфер обмена недоступен."); } }; section.Children.Add(copyLog);
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
    private void Report(string message)
    {
        _logText.AppendLine(message);
        var entry=new ConsoleEntry(message,ConsoleEntry.Classify(message));
        _log.Document.Blocks.Add(new Paragraph(new System.Windows.Documents.Run(message){Foreground=entry.Color}){Margin=new Thickness(0,0,0,2)});
        while(_log.Document.Blocks.Count>2500 && _log.Document.Blocks.FirstBlock is { } first)_log.Document.Blocks.Remove(first);
        if(!_log.IsKeyboardFocusWithin||_log.Selection.IsEmpty)_log.ScrollToEnd();
        _status.Text=message;_status.Foreground=entry.Color;
        if(message.StartsWith("[")&&message.IndexOf(']') is int end&&end>0&&_live.TryGetValue(message[1..end],out var r))
        {
            string line=message[(end+1)..].Trim();
            if(line=="START")r.Status="CHECKING";
            else if(line.StartsWith("CONNECTED"))r.Status="CONNECTED";
            else if(line.Contains("POST /auth status=200"))r.Status="AUTH OK";
            else if(line.StartsWith("Снимок ")){r.Samples=line.Split(' ').ElementAtOrDefault(1)??"";r.Status="COLLECTING";}
            else if(line.StartsWith("COMPLETE | OFFICE ")||line.StartsWith("PARTIAL | OFFICE ")){r.Status=line.Split('|')[0].Trim();r.Office=line.Split(' ').Last();}
            else if(line.StartsWith("FAILED:")||line=="CANCELLED")r.Status=line;
            else return;
            RefreshLive();
        }
    }
    private void RefreshLive()
    {
        _batchGrid.ItemsSource=_live.Values.ToArray();
        int done=_live.Values.Count(r=>r.Status is "COMPLETE" or "PARTIAL" or "CANCELLED" || r.Status.StartsWith("FAILED"));
        _batchProgress.Maximum=Math.Max(1,_live.Count);_batchProgress.Value=done;
        _batchSummary.Text=$"{done}/{_live.Count} | OK {_live.Values.Count(r=>r.Status=="COMPLETE")} | PARTIAL {_live.Values.Count(r=>r.Status=="PARTIAL")} | FAILED {_live.Values.Count(r=>r.Status.StartsWith("FAILED"))} | CANCELLED {_live.Values.Count(r=>r.Status=="CANCELLED")}";
    }
    private void EditLogin()
    {
        try
        {
            var addresses=BatchNatCollector.ParseAddresses(_address.Text,_https.IsChecked==true,_vpn.IsChecked==true);
            var dialog=new Window{Owner=this,Title=T("Per-router credentials","Учётные данные роутера"),Width=420,Height=300,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground};
            DarkTitleBar.Apply(dialog);var panel=new StackPanel{Margin=new Thickness(16)};dialog.Content=panel;
            var choice=new ComboBox{ItemsSource=addresses,SelectedIndex=0};panel.Children.Add(choice);
            panel.Children.Add(new TextBlock{Text=T("Username","Логин"),Margin=new Thickness(0,6,0,2)});
            var user=new TextBox{Text=_user.Text};panel.Children.Add(user);
            panel.Children.Add(new TextBlock{Text=T("Password (session only)","Пароль (только на эту сессию)"),Margin=new Thickness(0,6,0,2)});
            var pass=new PasswordBox();panel.Children.Add(pass);
            var save=new Button{Content=T("SET FOR ROUTER","ЗАДАТЬ ДЛЯ РОУТЕРА"),Style=(Style)FindResource("AccentButton"),Margin=new Thickness(0,10,0,0)};panel.Children.Add(save);
            var clear=new Button{Content=T("USE COMMON LOGIN","ИСПОЛЬЗОВАТЬ ОБЩИЙ ЛОГИН"),Style=(Style)FindResource("FlatButton"),Margin=new Thickness(0,5,0,0)};panel.Children.Add(clear);
            save.Click+=(_,_)=>{if(choice.SelectedItem is string a&&user.Text.Trim().Length>0&&pass.Password.Length>0){_overrides[a]=(user.Text.Trim(),pass.Password);pass.Clear();dialog.DialogResult=true;Report("READY: per-router login set for "+a);}};
            clear.Click+=(_,_)=>{if(choice.SelectedItem is string a)_overrides.Remove(a);pass.Clear();dialog.DialogResult=true;};
            dialog.ShowDialog();pass.Clear();
        }
        catch(Exception ex){Report("ERROR: "+ex.Message);}
    }
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
        bool retry=operation=="retry";if(retry&&_lastBatch==null)return;
        var prior=retry?_lastBatch:null;if(retry)operation="nat";
        var addr = Address; var user = _user.Text.Trim(); bool https = _https.IsChecked==true, vpn = _vpn.IsChecked==true;
        int samples = 3, interval = 5, detail = _period.SelectedIndex;
        string? import = null;
        string[] routers = [];
        try
        {
            if (operation is "nat" or "traffic" or "probe")
            {
                routers = retry ? prior!.Statuses.Where(s=>s.Status!="COMPLETE").Select(s=>s.Router).ToArray() : BatchNatCollector.ParseAddresses(_address.Text,https,vpn || operation=="probe");
                if(routers.Length==0){Report("READY: no incomplete routers");return;}
                foreach(var router in routers)if(vpn||operation=="probe")RemoteVpn.RequirePrivateTarget(Engine.ParseRouterUri(router,https));
                if(operation!="nat" && routers.Length!=1) throw new ArgumentException(T("TOP Traffic and TCP checks require one router. Use Collect NAT for a list.", "TOP Traffic и проверка TCP требуют один роутер. Для списка используйте «Собрать NAT»."));
                addr=routers[0];
                if (operation != "probe" && routers.Any(r=>!_overrides.ContainsKey(r)) && (user.Length == 0 || _password.Password.Length == 0)) throw new ArgumentException(T("Enter username and password.", "Введите логин и пароль."));
                if (operation != "probe" && !https && MessageBox.Show(this,T("HTTP is not encrypted. Continue only on an authorized trusted network or VPN?", "HTTP не шифруется. Продолжить только в разрешённой доверенной сети или VPN?"),Title,MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
            }
            if (operation == "demo") Engine.SetTargetSubnet(Engine.DemoSubnet);
            if (operation is "nat" or "import") { Engine.SetTargetSubnet(_target.Text); if(prior!=null&&prior.Target!=Engine.TargetSubnet)throw new ArgumentException("Retry requires the original target.");SaveTarget(Engine.TargetSubnet); }
            if (operation == "nat" && (!int.TryParse(_samples.Text,out samples) || samples is <1 or >12 || !int.TryParse(_interval.Text,out interval) || interval is <1 or >60)) throw new ArgumentException(T("Invalid sample settings.", "Некорректные параметры снимков."));
            if (operation == "import") { var dialog = new OpenFileDialog { Filter="CSV (*.csv)|*.csv", CheckFileExists=true }; if (dialog.ShowDialog(this)!=true) return; import=dialog.FileName; }
        }
        catch(Exception ex) { Report(ex.Message); return; }
        using var cts = new CancellationTokenSource(); _running=cts; _stop.IsEnabled=true;
        foreach(var b in _commands) b.IsEnabled=false;
        _show.IsEnabled=_excel.IsEnabled=_reports.IsEnabled=_retry.IsEnabled=_access.IsEnabled=false;
        if(operation=="nat"){_live.Clear();if(prior!=null)foreach(var s in prior.Statuses)_live[s.Router]=new RouterLive{Router=s.Router,Status=s.Status,Office=s.OfficeRows};foreach(var a in routers)_live[a]=new RouterLive{Router=a};RefreshLive();}
        _address.IsEnabled=_user.IsEnabled=_password.IsEnabled=_target.IsEnabled=_https.IsEnabled=_vpn.IsEnabled=_samples.IsEnabled=_interval.IsEnabled=_period.IsEnabled=false;
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
                var login=_overrides.TryGetValue(addr,out var local)?local:(user,secret);
                var result=await Task.Run(()=>TrafficMonitor.Run(addr,login.Item1,login.Item2,https,vpn,detail,cts.Token,log),cts.Token);
                SetReport(result.Folder,result.Excel,T("Traffic devices: ","Устройств в рейтинге: ")+result.Rows.Count,true);
            }
            else if(operation=="nat")
            {
                var credentials=new Dictionary<string,(string User,string Password)>(_overrides,StringComparer.OrdinalIgnoreCase);
                var result=await Task.Run(()=>BatchNatCollector.Run(routers,user,secret,https,vpn,samples,interval,ReportLocator.ResultsRoot,cts.Token,log,(a,ct,l)=>{var login=credentials.TryGetValue(a,out var local)?local:(user,secret);return Engine.Run(false,a,login.Item1,login.Item2,https,samples,interval,ct,l,vpn);},prior));
                credentials.Clear();_lastBatch=result;
                foreach(var s in result.Statuses)_live[s.Router]=new RouterLive{Router=s.Router,Status=s.Status,Office=s.OfficeRows,Samples=_live.TryGetValue(s.Router,out var current)?current.Samples:""};RefreshLive();
                var single=result.Statuses.Length==1&&result.Complete==1?result.Statuses[0].ReportFolder:null;
                SetReport(single??result.Folder,single==null?result.Excel:Path.Combine(single,"Keenetic_Network_Connections.xlsx"),T("Combined office rows: ","Общий отчёт в офис: ")+result.OfficeCount+$" | OK {result.Complete} | PARTIAL {result.Partial} | FAILED {result.Failed} | CANCELLED/NOT STARTED {result.Skipped}",false);
                if(string.IsNullOrEmpty(result.Excel)) Report(T("Excel unavailable; CSV is saved.","Excel недоступен; CSV сохранён."));
            }
            else
            {
                CollectionResult result = operation=="import" ? await Task.Run(()=>CentralFlowImporter.Import(import!,log,cts.Token),cts.Token) : await Task.Run(()=>Engine.Run(operation=="demo",addr,user,secret,https,samples,interval,cts.Token,log,vpn),cts.Token);
                SetReport(result.Folder,result.Excel,$"{result.Status} | ALL {result.All.Count} | TO OFFICE {result.Target.Count} | RELATED {result.OfficeRelated.Count}",false);
            }
        }
        catch(OperationCanceledException){Report(T("Cancelled.","Отменено."));}
        catch(Exception ex){Report(T("Operation failed: ","Ошибка операции: ")+ex.Message);}
        finally {secret="";_running=null; _stop.IsEnabled=false;foreach(var b in _commands)b.IsEnabled=true;_show.IsEnabled=_view.SelectedItem!=null;_excel.IsEnabled=File.Exists(_excelPath);_reports.IsEnabled=Directory.Exists(_folder);_retry.IsEnabled=_lastBatch?.Statuses.Any(s=>s.Status!="COMPLETE")==true;_access.IsEnabled=_folder!=null&&File.Exists(Path.Combine(_folder,"07_Office_To_Connections.csv"));_address.IsEnabled=_user.IsEnabled=_password.IsEnabled=_target.IsEnabled=_https.IsEnabled=_vpn.IsEnabled=_samples.IsEnabled=_interval.IsEnabled=_period.IsEnabled=true;}
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if(_running!=null){_running.Cancel();e.Cancel=true;}else{_overrides.Clear();_password.Clear();}base.OnClosing(e);
    }
}
