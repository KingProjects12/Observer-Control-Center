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
internal sealed class LegacyCollectorPanel : UserControl
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
    internal event Action<string>? LogLine;
    internal event Action? StateChanged;
    internal event Action<string,string>? NatReady;
    internal event Action<string,bool>? ReportReady;
    internal bool IsNatRunning=>IsBusy&&_runningOperation=="nat";
    private string _runningOperation="";
    private string _requestedMode="nat";
    private readonly Dictionary<string,(string Folder,string? Excel,string? Selected)> _reportsByMode=new();
    private string? _natFile;
    internal string? NatFile=>_natFile;
    internal event Action? NavigateAccess;
    internal event Action<string>? CsvSelected;
    internal bool IsBusy => _running!=null;
    internal int Processed => _live.Values.Count(r=>r.Status is "COMPLETE" or "PARTIAL" or "CANCELLED" || r.Status.StartsWith("FAILED"));
    internal int RouterCount => _live.Count;
    internal int OfficeCount => IsNatRunning?_live.Values.Sum(r=>int.TryParse(r.Office,out int n)?n:0):_lastBatch?.OfficeCount??0;
    internal int FailedCount => _live.Values.Count(r=>r.Status.StartsWith("FAILED"));
    internal int PartialCount => _live.Values.Count(r=>r.Status=="PARTIAL");
    internal object[] Issues => _live.Values.Where(r=>r.Status.StartsWith("FAILED")||r.Status is "PARTIAL" or "CANCELLED" or "NOT_STARTED").Select(r=>(object)new {r.Router,r.Status,r.Samples,r.Office}).ToArray();
    internal string Target => _target.Text;
    internal bool CanRetry => !IsBusy && _lastBatch?.Statuses.Any(s=>s.Status!="COMPLETE")==true;
    internal Task RetryAsync()=>Run("retry");
    internal void Stop()=>_running?.Cancel();
    internal void ClearSecrets(){_overrides.Clear();_password.Clear();}
    internal void SetAddress(string value){if(!IsBusy && !string.IsNullOrWhiteSpace(value))_address.Text=value;}
    private readonly bool _embedded,_offline;
    private readonly Expander _connection;
    private DataGrid? _preview;
    private TextBlock? _previewHint;
    private TextBlock? _modeHint;
    private FrameworkElement? _natSettings;
    private FrameworkElement? _trafficSettings;
    private FrameworkElement? _routerStatusArea;
    private bool _natFormExpanded=true,_trafficFormExpanded=true;
    private System.Windows.Controls.RadioButton? _httpRadio,_httpsRadio;
    private readonly Button _start;
    private readonly TextBlock _addressSummary;
    private string _operation="nat";
    private Window? DialogOwner=>Window.GetWindow(this);
    private string DialogTitle=>T("Observer · Network collector","Observer · Сбор данных");
    private static string ProtocolPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ObserverControlCenter","collector-protocol.txt");
    private static bool LoadHttps(){try{return !File.Exists(ProtocolPath)||File.ReadAllText(ProtocolPath).Trim()!="http";}catch{return true;}}
    private void SaveProtocol(){if(_offline)return;try{Directory.CreateDirectory(Path.GetDirectoryName(ProtocolPath)!);File.WriteAllText(ProtocolPath,_https.IsChecked==true?"https":"http");}catch(IOException){}catch(UnauthorizedAccessException){}}
    internal void SetMode(string operation,bool? vpn=null)
    {
        _requestedMode=operation;
        if(IsBusy)return;
        if(_operation!=operation)
        {
            if(_operation=="traffic")_trafficFormExpanded=_connection.IsExpanded;else _natFormExpanded=_connection.IsExpanded;
            if(_folder!=null)_reportsByMode[_operation]=(_folder,_excelPath,(_view.SelectedItem as View)?.File);
            _operation=operation;
            if(_reportsByMode.TryGetValue(operation,out var saved))
                ApplyReportView(saved.Folder,saved.Excel,operation=="traffic",saved.Selected);
            else ApplyReportView(null,null,false,null);
            _connection.IsExpanded=operation=="traffic"?_trafficFormExpanded:_natFormExpanded;
        }
        if(vpn.HasValue)_vpn.IsChecked=vpn.Value;
        _start.Content=operation=="traffic"?T("▶  Collect traffic","▶  Собрать трафик"):operation=="probe"?T("▶  Check router TCP","▶  Проверить TCP роутера"):T("▶  Start NAT collection","▶  Начать сбор NAT");
        _operation=operation;
        if(_natSettings!=null)_natSettings.Visibility=operation=="traffic"?Visibility.Collapsed:Visibility.Visible;
        if(_trafficSettings!=null)_trafficSettings.Visibility=operation=="traffic"?Visibility.Visible:Visibility.Collapsed;
        if(_routerStatusArea!=null)_routerStatusArea.Visibility=operation=="traffic"?Visibility.Collapsed:Visibility.Visible;
        if(_modeHint!=null)_modeHint.Text=operation=="traffic"?T("Choose one router and the observation window. Results show registered-device Internet volume, not all LAN/VPN traffic.","Выберите один роутер и период. Результаты показывают объём интернет-трафика зарегистрированных устройств, а не все LAN/VPN-потоки."):T("Collect observed NAT connections from authorized routers and match the office subnet.","Соберите наблюдаемые NAT-соединения разрешённых роутеров и отфильтруйте сеть офиса.");
    }
    public LegacyCollectorPanel(string address, bool vpn = false, bool traffic = false,bool embedded=false,bool offline=false)
    {
        _embedded=embedded;_offline=offline;
        SetResourceReference(ForegroundProperty,"DialogText");
        FontFamily = new FontFamily("Segoe UI");FontSize=14;
        var root = new StackPanel { Margin = new Thickness(embedded?4:18) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        StackPanel section = root;
        void Group(string en, string ru, string hintEn, string hintRu)
        {
            var card = new Border { Style=(Style)FindResource("CardBorder"), Margin=new Thickness(0,0,0,9),Padding=new Thickness(14) };
            section = new StackPanel(); card.Child=section; root.Children.Add(card);
            section.Children.Add(new TextBlock { Text=T(en,ru), FontSize=18, FontWeight=FontWeights.SemiBold });
            section.Children.Add(new TextBlock { Text=T(hintEn,hintRu), FontSize=13, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,4,0,8) });
        }
        TextBlock LabelFor(string value){var label=new TextBlock{Text=value,FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,5,9,5),TextWrapping=TextWrapping.Wrap};label.SetResourceReference(TextBlock.ForegroundProperty,"TextMain");return label;}
        void Label(string s)=>section.Children.Add(LabelFor(s));
        TextBox Field(string s,string v){Label(s);var b=new TextBox{Text=v};section.Children.Add(b);return b;}
        Group("Connection settings","Настройки подключения","Shared for all modules. Credentials stay in memory only.","Общие для модулей. Пароли хранятся только в памяти.");
        var form=new StackPanel();
        _connection=new Expander{IsExpanded=true,Content=form,Margin=new Thickness(0,2,0,5)};
        _addressSummary=new TextBlock{FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
        _connection.Header=_addressSummary;section.Children.Add(_connection);
        _address=new TextBox{Text=address,AcceptsReturn=true,MinHeight=66,MaxHeight=112,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),FontSize=14};
        var list=new Expander{Header=T("Edit router list (one IPv4 or IPv4:port per line)","Редактировать список (IPv4 или IPv4:порт по строкам)"),IsExpanded=string.IsNullOrWhiteSpace(address),Content=_address};
        var addressRow=new DockPanel{Margin=new Thickness(0,4,0,5)};
        var importList=new Button{Content=T("Import .txt","Импорт .txt"),Style=(Style)FindResource("FlatButton"),Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Top};
        DockPanel.SetDock(importList,Dock.Right);addressRow.Children.Add(importList);addressRow.Children.Add(list);form.Children.Add(addressRow);
        importList.Click+=(_,_)=>{try{var d=new OpenFileDialog{Filter="Text (*.txt)|*.txt",CheckFileExists=true};if((DialogOwner is Window owner?d.ShowDialog(owner):d.ShowDialog())==true){if(new FileInfo(d.FileName).Length>65536)throw new InvalidDataException("IP list exceeds 64 KB.");_address.Text=File.ReadAllText(d.FileName);list.IsExpanded=true;}}catch(Exception ex){Report("ERROR: "+ex.Message);}};
        var auth=new Grid{Margin=new Thickness(0,3,0,5)};
        auth.ColumnDefinitions.Add(new ColumnDefinition());auth.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});auth.ColumnDefinitions.Add(new ColumnDefinition());
        var userPanel=new StackPanel();userPanel.Children.Add(LabelFor(T("Username","Логин")));_user=new TextBox();userPanel.Children.Add(_user);auth.Children.Add(userPanel);
        var passPanel=new StackPanel();Grid.SetColumn(passPanel,2);passPanel.Children.Add(LabelFor(T("Password (session only)","Пароль (только сессия)")));_password=new PasswordBox();passPanel.Children.Add(_password);auth.Children.Add(passPanel);form.Children.Add(auth);
        var individual=new Button{Content=T("Individual router credentials","Индивидуальные данные роутеров"),Style=(Style)FindResource("FlatButton"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,4)};
        individual.Click+=(_,_)=>EditLogin();form.Children.Add(individual);
        var network=new WrapPanel{Margin=new Thickness(0,3,0,4)};
        network.Children.Add(LabelFor(T("Office /24","Сеть офиса /24")));_target=new TextBox{Width=165,Text=LoadTarget(),Margin=new Thickness(0,0,10,0)};network.Children.Add(_target);
        _https=new CheckBox{IsChecked=LoadHttps(),Visibility=Visibility.Collapsed};network.Children.Add(_https);
        form.Children.Add(network);
        var protocolRow=new WrapPanel{Margin=new Thickness(0,6,0,4)};
        protocolRow.Children.Add(LabelFor(T("Protocol","Протокол")));
        _httpRadio=new RadioButton{Content="HTTP",GroupName=Guid.NewGuid().ToString(),IsChecked=_https.IsChecked!=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};
        _httpsRadio=new RadioButton{Content="HTTPS",GroupName=_httpRadio.GroupName,IsChecked=_https.IsChecked==true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)};
        foreach(var radio in new[]{_httpRadio,_httpsRadio}){radio.SetResourceReference(Control.ForegroundProperty,"TextMain");protocolRow.Children.Add(radio);}
        _httpRadio.Checked+=(_,_)=>_https.IsChecked=false;_httpsRadio.Checked+=(_,_)=>_https.IsChecked=true;
        _vpn=new CheckBox{Content="VPN REMOTE",IsChecked=vpn,VerticalAlignment=VerticalAlignment.Center};protocolRow.Children.Add(_vpn);form.Children.Add(protocolRow);
        var advanced=new Expander{Header=T("Advanced settings: NAT snapshots, intervals, traffic period","Дополнительно: снимки NAT, интервалы, период трафика"),Margin=new Thickness(0,6,0,3)};
        var advancedFields=new WrapPanel{Margin=new Thickness(0,7,0,0)};
        advancedFields.Children.Add(LabelFor(T("Snapshots (1–12)","Снимки (1–12)")));_samples=new TextBox{Text="3",Width=52,Margin=new Thickness(0,0,10,0)};advancedFields.Children.Add(_samples);
        advancedFields.Children.Add(LabelFor(T("Interval (s)","Интервал (с)")));_interval=new TextBox{Text="5",Width=52,Margin=new Thickness(0,0,10,0)};advancedFields.Children.Add(_interval);
        advanced.Content=advancedFields;form.Children.Add(advanced);_natSettings=advanced;
        var periodRow=new WrapPanel{Margin=new Thickness(0,5,0,0)};periodRow.Children.Add(LabelFor(T("Traffic window","Период трафика")));
        _period=new ComboBox{Width=150,ItemsSource=new[]{T("3 minutes","3 минуты"),T("1 hour","1 час"),T("3 hours","3 часа"),T("24 hours","24 часа")},SelectedIndex=traffic?3:0};periodRow.Children.Add(_period);form.Children.Add(periodRow);_trafficSettings=periodRow;
        void UpdateConnectionSummary(){_addressSummary.Text=T("Selected routers: ","Выбрано роутеров: ")+BatchNatCollector.Split(_address.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count()+"  •  "+(_https.IsChecked==true?"HTTPS":"HTTP")+"  •  VPN "+(_vpn.IsChecked==true?"ON":"OFF")+"  •  "+_target.Text;}
        _address.TextChanged+=(_,_)=>UpdateConnectionSummary();_target.TextChanged+=(_,_)=>UpdateConnectionSummary();_vpn.Checked+=(_,_)=>UpdateConnectionSummary();_vpn.Unchecked+=(_,_)=>UpdateConnectionSummary();
        _https.Checked+=(_,_)=>{UpdateConnectionSummary();SaveProtocol();};_https.Unchecked+=(_,_)=>{UpdateConnectionSummary();SaveProtocol();};UpdateConnectionSummary();
        Group("Collection", "Сбор данных", "Choose a collection operation.", "Выберите операцию сбора.");
        _modeHint=(TextBlock)section.Children[1];
        var buttons = new WrapPanel { Margin = new Thickness(0,4,0,2) }; section.Children.Add(buttons);
        Button Button(string label, RoutedEventHandler handler, bool accent = false, string? hint = null)
        {
            var b = new Button { Content = label, Style = (Style)FindResource(accent ? "AccentButton" : "FlatButton"), Margin = new Thickness(0,0,8,8), ToolTip=hint }; b.Click += handler; buttons.Children.Add(b); return b;
        }
        _start=Button(T("▶  Start NAT collection","▶  Начать сбор NAT"),async (_,_)=>await Run(_operation),true);_start.FontSize=15;_start.Padding=new Thickness(16,10,16,10);_commands.Add(_start);
        _commands.Add(Button(T("VPN / CHECK TCP","VPN / ПРОВЕРИТЬ TCP"),async (_,_)=>await Run("probe"),false,T("Test the configured router port, not a VPN tunnel.","Проверка порта роутера, а не VPN-туннеля.")));
        _commands.Add(Button(T("TOOLS ▾", "ИНСТРУМЕНТЫ ▾"), (sender,_) => {
            var menu = new ContextMenu { PlacementTarget=(UIElement)sender, Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom };
            var importItem = new MenuItem { Header=T("Import central flow CSV", "Импорт CSV центрального шлюза") }; importItem.Click += async (_,_) => await Run("import"); menu.Items.Add(importItem);
            var natItem=new MenuItem{Header=T("Collect NAT","Собрать NAT")};natItem.Click+=async (_,_)=>await Run("nat");menu.Items.Add(natItem);
            var trafficItem=new MenuItem{Header=T("TOP Traffic","ТОП трафика")};trafficItem.Click+=async (_,_)=>await Run("traffic");menu.Items.Add(trafficItem);
            var demoItem = new MenuItem { Header=T("Offline demo test", "Офлайн демо-тест") }; demoItem.Click += async (_,_) => await Run("demo"); menu.Items.Add(demoItem);
            menu.IsOpen=true;
        }));
        _commands.Add(individual);_commands.Add(importList);
        _retry=Button(T("RETRY INCOMPLETE", "ПОВТОРИТЬ ПРОБЛЕМНЫЕ"), async (_,_)=>await Run("retry"));_retry.IsEnabled=false;
        _stop = Button(T("STOP", "СТОП"), (_,_) => _running?.Cancel()); _stop.IsEnabled = false; _stop.ToolTip=T("Cancel collection and retain available results.","Остановить сбор, сохранить доступные результаты.");
        var statusArea=new StackPanel();_routerStatusArea=statusArea;
        _batchSummary=new TextBlock{Text=T("No collection started","Сбор ещё не запускался"),TextWrapping=TextWrapping.Wrap,Foreground = (Brush)FindResource("DialogAccent"),Margin=new Thickness(0,8,0,4)};statusArea.Children.Add(_batchSummary);
        _batchProgress=new ProgressBar{Minimum=0,Maximum=1,Height=6,Margin=new Thickness(0,3,0,8)};statusArea.Children.Add(_batchProgress);
        _batchGrid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,MaxHeight=180,MinHeight=64,RowHeight=31,FontSize=13};
        foreach(string key in new[]{"Router","Status","Samples","Office"}){var c=new DataGridTextColumn{Header=key switch{"Router"=>T("Router","Роутер"),"Status"=>T("Status","Статус"),"Samples"=>T("Snapshots","Снимки"),_=>T("Office","В офис")},Binding=new Binding(key),Width=new DataGridLength(1,DataGridLengthUnitType.Star)};if(key=="Status"){var st=new Style(typeof(TextBlock));st.Setters.Add(new Setter(TextBlock.ForegroundProperty,new Binding(key){Converter=new StatusBrushConverter()}));c.ElementStyle=st;}_batchGrid.Columns.Add(c);}statusArea.Children.Add(_batchGrid);
        var statusExpander=new Expander{Header=T("Router status","Статусы роутеров"),IsExpanded=!embedded,Content=statusArea,Margin=new Thickness(0,4,0,0)};section.Children.Add(statusExpander);_routerStatusArea=statusExpander;
        Group("Reports", "Отчёты", "Select a report after collection, then view it or open Excel.", "После сбора выберите отчёт и откройте его в Observer или Excel.");
        Label(T("Report view", "Отображение отчёта"));
        _view = new ComboBox { DisplayMemberPath = "Label",MaxWidth=420,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=230 }; section.Children.Add(_view);
        _view.SelectionChanged += (_,_) => { if (_show != null) _show.IsEnabled = _running == null && _view.SelectedItem != null; RefreshPreview(); };
        var output = new WrapPanel { Margin = new Thickness(0,10,0,4) }; section.Children.Add(output); buttons = output;
        _show = Button(T("SHOW IN OBSERVER", "ПОКАЗАТЬ В OBSERVER"), (_,_) => { if (_view.SelectedItem is View v && _folder != null) { SelectedCsv = Path.Combine(_folder,v.File); CsvSelected?.Invoke(SelectedCsv); } }); _show.IsEnabled = false; _show.ToolTip=T("Open the selected CSV in the main table.","Показать выбранный CSV в главной таблице.");
        _excel = Button(T("OPEN EXCEL", "ОТКРЫТЬ EXCEL"), (_,_) => Open(_excelPath)); _excel.IsEnabled = false; _excel.ToolTip=T("Open the generated Excel workbook.","Открыть сформированный файл Excel.");
        _reports = Button(T("OPEN REPORTS", "ОТКРЫТЬ ОТЧЁТЫ"), (_,_) => Open(_folder)); _reports.IsEnabled = false; _reports.ToolTip=T("Open the current report folder.","Открыть папку текущего отчёта.");
        _access=Button(T("ANALYZE ACCESS", "АНАЛИЗ ДОСТУПОВ"),(_,_)=>{var csv=_folder==null?null:Path.Combine(_folder,"07_Office_To_Connections.csv");if(_embedded){NavigateAccess?.Invoke();}else{new AccessAnalyzerWindow(csv,_target.Text){Owner=DialogOwner}.ShowDialog();}});_access.IsEnabled=false;
        Group("05 / ACTIVITY", "05 / ЖУРНАЛ", "Progress and diagnostic details. Copy the log when troubleshooting.", "Прогресс и диагностика. При ошибке скопируйте журнал.");
        _status = new TextBlock { Text = T("Ready. Choose NAT for destinations; TOP Traffic for volume.", "Готово. NAT — адреса назначения; TOP Traffic — объём трафика."), TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("DialogAccent") }; section.Children.Add(_status);
        _log = new RichTextBox { IsReadOnly = true, MinHeight = 140, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = (Brush)FindResource("DialogLog"), Foreground = (Brush)FindResource("DialogText"), Margin = new Thickness(0,8,0,0) }; section.Children.Add(_log);
        var copyLog = new Button { Content=T("COPY LOG","КОПИРОВАТЬ ЖУРНАЛ"), Style=(Style)FindResource("FlatButton"), HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,8,0,0) };
        copyLog.Click += (_,_) => { if(_logText.Length==0)return; try { Clipboard.SetText(_logText.ToString()); } catch(System.Runtime.InteropServices.COMException) { _status.Text=T("Clipboard unavailable.","Буфер обмена недоступен."); } }; section.Children.Add(copyLog);
        Label(T("Read-only. The existing VPN must already be connected. NAT and TOP Traffic do not prove all VPN/LAN flows. Raw reports are confidential.", "Только чтение. VPN должен уже существовать и быть подключён. NAT и TOP Traffic не показывают гарантированно все VPN/LAN-потоки. Сырые отчёты конфиденциальны."));
        if(embedded)
        {
            var connectionCard=(Border)root.Children[0];var actionCard=(Border)root.Children[1];var reportCard=(Border)root.Children[2];
            root.Children.Clear();
            // Each tool panel can be reduced to its header without holding space in the results area.
            void MakeCollapsible(Border card,string label)
            {
                var body=(StackPanel)card.Child;card.Child=null;body.Children.RemoveAt(0);
                card.Child=new Expander{Header=label,IsExpanded=true,Content=body};
            }
            MakeCollapsible(actionCard,T("Collection","Сбор данных"));
            MakeCollapsible(reportCard,T("Reports and export","Отчёты и экспорт"));
            var workspace=new Grid{Margin=new Thickness(6)};
            var leftCol=new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star),MinWidth=290,MaxWidth=510};workspace.ColumnDefinitions.Add(leftCol);workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3,GridUnitType.Star)});
            var left=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=connectionCard};workspace.Children.Add(left);
            var right=new Grid();Grid.SetColumn(right,2);workspace.Children.Add(right);
            right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());
            right.Children.Add(actionCard);Grid.SetRow(reportCard,1);right.Children.Add(reportCard);
            var previewCard=new Border{Style=(Style)FindResource("CardBorder"),Padding=new Thickness(12),Margin=new Thickness(0)};
            var previewRoot=new DockPanel();
            _previewHint=new TextBlock{Text=T("No report yet — run collection to see actual data.","Отчёта пока нет — запустите сбор, чтобы увидеть данные."),TextWrapping=TextWrapping.Wrap,FontSize=13,Margin=new Thickness(0,0,0,9)};_previewHint.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");DockPanel.SetDock(_previewHint,Dock.Top);previewRoot.Children.Add(_previewHint);
            _preview=new DataGrid{AutoGenerateColumns=true,IsReadOnly=true,CanUserAddRows=false,FontSize=13,RowHeight=32,ColumnHeaderHeight=35};previewRoot.Children.Add(_preview);
            previewCard.Child=previewRoot;Grid.SetRow(previewCard,2);right.Children.Add(previewCard);
            void ResizeConnection(){leftCol.MinWidth=_connection.IsExpanded?290:180;leftCol.MaxWidth=_connection.IsExpanded?510:220;leftCol.Width=_connection.IsExpanded?new GridLength(2,GridUnitType.Star):new GridLength(210);}
            _connection.Expanded+=(_,_)=>ResizeConnection();_connection.Collapsed+=(_,_)=>ResizeConnection();ResizeConnection();
            Content=workspace;
        }
        SetMode(traffic?"traffic":"nat",vpn);
        PanelPalette.Bind(this);
    }
    private void RefreshPreview()
    {
        if(_preview==null||_previewHint==null)return;
        _preview.ItemsSource=null;
        if(_folder==null||_view.SelectedItem is not View v){_previewHint.Text=T("No report yet — run collection to see actual data.","Отчёта пока нет — запустите сбор, чтобы увидеть данные.");return;}
        try{var file=Path.Combine(_folder,v.File);var table=CsvTableReader.ReadTable(file);_preview.ItemsSource=table.DefaultView;_previewHint.Text=v.Label+"  •  "+table.Rows.Count+" "+T("rows","строк");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException){_previewHint.Text=T("Preview unavailable: ","Предпросмотр недоступен: ")+ex.Message;}
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
        _logText.AppendLine(message); LogLine?.Invoke(message);
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
        StateChanged?.Invoke();
    }
    private void EditLogin()
    {
        try
        {
            var addresses=BatchNatCollector.ParseAddresses(_address.Text,_https.IsChecked==true,_vpn.IsChecked==true);
            var dialog=new Window{Owner=DialogOwner,Title=T("Per-router credentials","Учётные данные роутера"),Width=420,Height=300,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground};
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
        string mode=traffic?"traffic":"nat";
        _reportsByMode[mode]=(folder,excel,null);
        if(mode==_operation)ApplyReportView(folder,excel,traffic,null);
        Report(status);
        var file=Path.Combine(folder,traffic?"08_Top_Traffic.csv":"07_Office_To_Connections.csv");
        if(File.Exists(file))
        {
            if(!traffic){_natFile=file;NatReady?.Invoke(file,Engine.TargetSubnet);}
            ReportReady?.Invoke(file,traffic);
        }
    }
    private void ApplyReportView(string? folder,string? excel,bool traffic,string? selected)
    {
        _folder=folder;_excelPath=excel;
        var views=folder==null?new List<View>():Views.Where(v=>File.Exists(Path.Combine(folder,v.File))).Select(v=>new View(v.File,T(v.En,v.Ru))).ToList();
        _view.ItemsSource=views;_view.SelectedItem=views.FirstOrDefault(v=>v.File==selected)??(traffic?views.FirstOrDefault(v=>v.File=="08_Top_Traffic.csv"):views.FirstOrDefault());
        _show.IsEnabled=!IsBusy&&_view.SelectedItem!=null;_excel.IsEnabled=!IsBusy&&File.Exists(_excelPath);_reports.IsEnabled=!IsBusy&&Directory.Exists(_folder);_access.IsEnabled=!IsBusy&&_folder!=null&&File.Exists(Path.Combine(_folder,"07_Office_To_Connections.csv"));
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
                if (operation != "probe" && !https && MessageBox.Show(DialogOwner!,T("HTTP is not encrypted. Continue only on an authorized trusted network or VPN?", "HTTP не шифруется. Продолжить только в разрешённой доверенной сети или VPN?"),DialogTitle,MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
            }
            if (operation == "demo") Engine.SetTargetSubnet(Engine.DemoSubnet);
            if (operation is "nat" or "import") { Engine.SetTargetSubnet(_target.Text); if(prior!=null&&prior.Target!=Engine.TargetSubnet)throw new ArgumentException("Retry requires the original target.");SaveTarget(Engine.TargetSubnet); }
            if (operation == "nat" && (!int.TryParse(_samples.Text,out samples) || samples is <1 or >12 || !int.TryParse(_interval.Text,out interval) || interval is <1 or >60)) throw new ArgumentException(T("Invalid sample settings.", "Некорректные параметры снимков."));
            if (operation == "import") { var dialog = new OpenFileDialog { Filter="CSV (*.csv)|*.csv", CheckFileExists=true }; if ((DialogOwner is Window owner?dialog.ShowDialog(owner):dialog.ShowDialog())!=true) return; import=dialog.FileName; }
        }
        catch(Exception ex) { Report(ex.Message); return; }
        using var cts = new CancellationTokenSource(); _running=cts; _runningOperation=operation; _stop.IsEnabled=true; _connection.IsExpanded=false; StateChanged?.Invoke();
        foreach(var b in _commands) b.IsEnabled=false;
        _show.IsEnabled=_excel.IsEnabled=_reports.IsEnabled=_retry.IsEnabled=_access.IsEnabled=false;
        if(operation=="nat"){_live.Clear();if(prior!=null)foreach(var s in prior.Statuses)_live[s.Router]=new RouterLive{Router=s.Router,Status=s.Status,Office=s.OfficeRows};foreach(var a in routers)_live[a]=new RouterLive{Router=a};RefreshLive();}
        _address.IsEnabled=_user.IsEnabled=_password.IsEnabled=_target.IsEnabled=_https.IsEnabled=_vpn.IsEnabled=_samples.IsEnabled=_interval.IsEnabled=_period.IsEnabled=false;
        if(_httpRadio!=null)_httpRadio.IsEnabled=false;if(_httpsRadio!=null)_httpsRadio.IsEnabled=false;
        string secret = _password.Password; if(!_embedded)_password.Clear();
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
        catch(OperationCanceledException) when(cts.IsCancellationRequested){Report("CANCELLED: "+T("Stopped by user.","Остановлено пользователем."));}
        catch(Exception ex){var kind=Engine.Classify(ex,cts.Token);Report(kind+": "+Engine.Friendly(ex,kind)+" | "+ex.Message);}
        finally {secret="";_running=null;_runningOperation=""; _stop.IsEnabled=false;foreach(var b in _commands)b.IsEnabled=true;_show.IsEnabled=_view.SelectedItem!=null;_excel.IsEnabled=File.Exists(_excelPath);_reports.IsEnabled=Directory.Exists(_folder);_retry.IsEnabled=_lastBatch?.Statuses.Any(s=>s.Status!="COMPLETE")==true;_access.IsEnabled=_folder!=null&&File.Exists(Path.Combine(_folder,"07_Office_To_Connections.csv"));_address.IsEnabled=_user.IsEnabled=_password.IsEnabled=_target.IsEnabled=_https.IsEnabled=_vpn.IsEnabled=_samples.IsEnabled=_interval.IsEnabled=_period.IsEnabled=true;if(_httpRadio!=null)_httpRadio.IsEnabled=true;if(_httpsRadio!=null)_httpsRadio.IsEnabled=true;SetMode(_requestedMode);StateChanged?.Invoke();}
    }
}

// Compatibility window for the old standalone/offline entry points.
internal sealed class LegacyCollectorWindow : Window
{
    private readonly LegacyCollectorPanel _panel;
    public string Address=>_panel.Address;
    public string? SelectedCsv=>_panel.SelectedCsv;
    public string? ReportFolder=>_panel.ReportFolder;
    public LegacyCollectorWindow(string address,bool vpn=false,bool traffic=false)
    {
        Title=UiText.T("Observer · Network collector","Observer · Сбор данных");Width=810;Height=880;MinWidth=600;MinHeight=600;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;DarkTitleBar.Apply(this);
        SetResourceReference(BackgroundProperty,"DialogBg");SetResourceReference(ForegroundProperty,"DialogText");
        _panel=new LegacyCollectorPanel(address,vpn,traffic);Content=_panel;
        _panel.CsvSelected+=_=>{DialogResult=true;};
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {if(_panel.IsBusy){_panel.Stop();e.Cancel=true;}else _panel.ClearSecrets();base.OnClosing(e);}
}
