using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using KeeneticObserver;
using Microsoft.Win32;
using Observer.ControlCenter.Models;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

internal sealed class AccessAnalyzerPanel : UserControl
{
    private readonly TextBox _target,_files,_filter;
    private readonly TextBlock _summary,_status;
    private TextBlock? _modeTitle,_modeDescription,_empty;
    private Expander? _filesExpander,_toolsExpander,_resultsExpander;
    private bool _connectionsToolsExpanded=true,_tcpToolsExpanded=true,_connectionsResultsExpanded=true,_tcpResultsExpanded=true;
    private bool _connectionsFilesExpanded=true,_tcpFilesExpanded=false;
    private readonly bool _embedded;
    private readonly DataGrid _grid;
    private readonly ComboBox _view;
    private readonly Button _analyze,_compare,_check,_export,_open,_stop,_pick;
    private readonly List<string> _paths=new();
    private AccessAnalysis? _analysis;
    private CancellationTokenSource? _running;
    private string? _output;
    private static string T(string en,string ru)=>UiText.T(en,ru);
    internal event Action<string>? LogLine;
    internal event Action? StateChanged;
    internal string OperationName { get; private set; } = "";
    internal string LastStatus => _status.Text;
    internal bool IsBusy=>_running!=null;
    internal int? OpenTcp=>_analysis==null||!_analysis.Endpoints.Any(r=>r.CheckedAt.Length>0&&r.Protocol=="TCP")?null:_analysis.Endpoints.Count(r=>r.TcpStatus=="TCP OPEN");
    internal void Stop()=>_running?.Cancel();
    internal void SetInput(string path,string target)
    {
        if(IsBusy||!File.Exists(path))return;
        if(_paths.Count==1&&string.Equals(_paths[0],path,StringComparison.OrdinalIgnoreCase)&&_target.Text==target)return;
        _paths.Clear();_paths.Add(path);_target.Text=target;ShowPaths();SetBusy(false);
        Status(T("READY: current NAT CSV loaded.","READY: текущий CSV NAT загружен."));
    }
    private int _connectionsView;
    private bool _tcpMode;
    internal void SetTcpMode(bool tcp)
    {
        if(_grid==null)return;
        bool changed=_tcpMode!=tcp;
        if(changed)
        {
            if(_filesExpander!=null){if(_tcpMode)_tcpFilesExpanded=_filesExpander.IsExpanded;else _connectionsFilesExpanded=_filesExpander.IsExpanded;}
            if(_toolsExpander!=null&&_resultsExpander!=null){if(_tcpMode){_tcpToolsExpanded=_toolsExpander.IsExpanded;_tcpResultsExpanded=_resultsExpander.IsExpanded;}else{_connectionsToolsExpanded=_toolsExpander.IsExpanded;_connectionsResultsExpanded=_resultsExpander.IsExpanded;}}
            if(tcp){_connectionsView=_view.SelectedIndex;_view.SelectedIndex=0;}else _view.SelectedIndex=_connectionsView;
        }
        _tcpMode=tcp;
        if(_modeTitle!=null)_modeTitle.Text=tcp?T("TCP availability","Доступность TCP"):T("Observed connections","Наблюдаемые соединения");
        if(_modeDescription!=null)_modeDescription.Text=tcp?T("Check only authorized observed TCP destinations; UDP is observation only.","Проверка только разрешённых наблюдаемых TCP-назначений. UDP — только наблюдения."):T("Analyze collected NAT CSVs and compare observations between periods.","Анализируйте собранные CSV NAT и сравнивайте наблюдения за периоды.");
        _compare.Visibility=tcp?Visibility.Collapsed:Visibility.Visible;_check.Visibility=tcp||!_embedded?Visibility.Visible:Visibility.Collapsed;
        _view.Visibility=tcp?Visibility.Collapsed:Visibility.Visible;
        if(changed&&_filesExpander!=null)_filesExpander.IsExpanded=tcp?_tcpFilesExpanded:_connectionsFilesExpanded;
        if(changed&&_toolsExpander!=null&&_resultsExpander!=null){_toolsExpander.IsExpanded=tcp?_tcpToolsExpanded:_connectionsToolsExpanded;_resultsExpander.IsExpanded=tcp?_tcpResultsExpanded:_connectionsResultsExpanded;}
        _analyze.Content=tcp?T("Prepare endpoints","Подготовить назначения"):T("Analyze CSV","Анализировать CSV");
        RefreshGrid();
    }
    internal Task AnalyzeCurrentAsync()=>Analyze(false);
    internal Task SaveCurrentAsync()=>Export();
    internal bool HasAnalysis=>_analysis!=null;
    private Window? DialogOwner=>Window.GetWindow(this);
    public AccessAnalyzerPanel(string? initialFile=null,string? target=null,bool embedded=false)
    {
        _embedded=embedded;
        SetResourceReference(ForegroundProperty,"DialogText");
        FontSize=14;
        var root=new DockPanel{Margin=new Thickness(10)};Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        _modeTitle=new TextBlock{FontSize=20,FontWeight=FontWeights.SemiBold};top.Children.Add(_modeTitle);
        _modeDescription=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,12),FontSize=13};_modeDescription.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");top.Children.Add(_modeDescription);
        var bar=new WrapPanel{Margin=new Thickness(0,0,0,5)};top.Children.Add(bar);
        bar.Children.Add(new TextBlock{Text=T("Office /24","Сеть офиса /24"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
        _target=new TextBox{Width=180,Text=target??LoadTarget(),Margin=new Thickness(0,0,8,0)};bar.Children.Add(_target);
        Button Add(string text,RoutedEventHandler click,bool accent=false)
        {
            var b=new Button{Content=text,Style=(Style)FindResource(accent?"AccentButton":"FlatButton"),Margin=new Thickness(0,0,8,6)};b.Click+=click;bar.Children.Add(b);return b;
        }
        _pick=Add(T("Select CSV","Выбрать CSV"),(_,_)=>Pick());
        _analyze=Add(T("Analyze","Анализировать"),async(_,_)=>await Analyze(false),true);
        _compare=Add(T("Compare periods","Сравнить периоды"),async(_,_)=>await Analyze(true));
        _check=Add(T("Check TCP","Проверить TCP"),async(_,_)=>await Check(),true);
        _stop=Add(T("Stop","Стоп"),(_,_)=>_running?.Cancel());
        _export=Add(T("Export CSV / XLSX","Экспорт CSV / XLSX"),async(_,_)=>await Export());
        _open=Add(T("Open reports","Открыть отчёты"),(_,_)=>{if(_output!=null&&Directory.Exists(_output))Process.Start(new ProcessStartInfo(_output){UseShellExecute=true});});
        _files=new TextBox{IsReadOnly=true,AcceptsReturn=true,MinHeight=36,MaxHeight=72,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,5)};
        _filesExpander=new Expander{Header=T("Input reports","Исходные отчёты"),IsExpanded=true,Content=_files,Margin=new Thickness(0,3,0,5)};top.Children.Add(_filesExpander);
        _summary=new TextBlock{Foreground = (Brush)FindResource("DialogAccent"),TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,3,0,5)};top.Children.Add(_summary);
        _status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,5)};top.Children.Add(_status);
        var controls=new DockPanel{Margin=new Thickness(0,4,0,6)};top.Children.Add(controls);
        _view=new ComboBox{Width=205,ItemsSource=new[]{T("Office endpoints","IP / порт / протокол"),T("Source rules","Источники → офис"),T("Import audit","Аудит импорта"),T("Endpoint changes","Изменения назначений"),T("Source changes","Изменения источников")},SelectedIndex=0,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(_view,Dock.Left);controls.Children.Add(_view);
        _filter=new TextBox{ToolTip=T("Search all displayed columns","Поиск по всем столбцам"),Margin=new Thickness(0,0,0,0)};controls.Children.Add(_filter);
        _grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Extended,FontSize=14,RowHeight=36,ColumnHeaderHeight=36};
        _grid.AutoGeneratingColumn+=(_,e)=>{
            e.Column.Header = e.PropertyName switch
            {
                "SourceIP" => T("Source IP","IP источника"),
                "SourceName" => T("Source name","Имя источника"),
                "SourceMAC" => T("Source MAC","MAC источника"),
                "DestinationIP" => T("Destination IP","IP назначения"),
                "DestinationPort" => T("Destination port","Порт назначения"),
                "FirstSeen" => T("First seen","Первое наблюдение"),
                "LastSeen" => T("Last seen","Последнее наблюдение"),
                "TcpStatus" => T("TCP status","Статус TCP"),
                "CheckDetail" => T("Check detail","Детали проверки"),
                "CheckedAt" => T("Checked at","Время проверки"),
                "ServiceHint" => T("Service hint","Сервис"),
                "ReverseOrOutside" => T("Reverse or outside","Обратные и внешние"),
                "Router" => T("Router","Роутер"),
                "Port" => T("Port","Порт"),
                "Protocol" => T("Protocol","Протокол"),
                "Observations" => T("Observations","Наблюдения"),
                "Sources" => T("Sources","Источники"),
                "Routers" => T("Routers","Роутеры"),
                "Review" => T("Review","Проверка"),
                "File" => T("File","Файл"),
                "Result" => T("Result","Результат"),
                "Rows" => T("Rows","Строки"),
                "Accepted" => T("Accepted","Принято"),
                "Duplicate" => T("Duplicates","Дубликаты"),
                "Invalid" => T("Invalid","Некорректные"),
                "Unsupported" => T("Unsupported","Не поддерживается"),
                "Earlier" => T("Earlier period","Ранний период"),
                "Later" => T("Later period","Поздний период"),
                "Change" => T("Change","Изменение"),
                _ => e.PropertyName
            };
            if(e.PropertyName is "TcpStatus" or "Change" or "Result")
            {
                var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.ForegroundProperty,new Binding(e.PropertyName){Converter=new StatusBrushConverter()}));
                if(e.Column is DataGridTextColumn c)c.ElementStyle=style;
            }
        };
        var gridArea=new Grid();gridArea.Children.Add(_grid);
        _empty=new TextBlock{TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,FontSize=15,Margin=new Thickness(18),IsHitTestVisible=false};_empty.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");gridArea.Children.Add(_empty);root.Children.Add(gridArea);
        if(embedded)
        {
            root.Children.Clear();
            top.Children.Remove(bar);top.Children.Remove(controls);top.Children.Remove(_modeTitle);top.Children.Remove(_summary);top.Children.Remove(_status);
            _toolsExpander=new Expander{Header=T("Analysis settings and actions","Настройки и действия"),IsExpanded=true,Content=top};
            _resultsExpander=new Expander{Header=T("Network results","Результаты сети"),IsExpanded=true,Content=gridArea};
            var workspace=new Grid();workspace.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});workspace.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});workspace.RowDefinitions.Add(new RowDefinition());workspace.RowDefinitions.Add(new RowDefinition{Height=new GridLength(0)});
            var toolsCard=new Border{Style=(Style)FindResource("WorkspaceSection"),Padding=new Thickness(8),Margin=new Thickness(0,0,0,8),Child=_toolsExpander};
            var resultsCard=new Border{Style=(Style)FindResource("WorkspaceSection"),Padding=new Thickness(8),Child=_resultsExpander};
            var actions=new StackPanel();actions.Children.Add(bar);actions.Children.Add(_summary);actions.Children.Add(_status);actions.Children.Add(controls);
            var actionsCard=new Border{Style=(Style)FindResource("WorkspaceSection"),Padding=new Thickness(8),Margin=new Thickness(0,0,0,8),Child=actions};
            workspace.Children.Add(toolsCard);Grid.SetRow(actionsCard,1);workspace.Children.Add(actionsCard);Grid.SetRow(resultsCard,2);workspace.Children.Add(resultsCard);
            void Reflow(){workspace.RowDefinitions[2].Height=_resultsExpander.IsExpanded?new GridLength(1,GridUnitType.Star):GridLength.Auto;workspace.RowDefinitions[3].Height=_resultsExpander.IsExpanded?new GridLength(0):new GridLength(1,GridUnitType.Star);}
            _resultsExpander.Expanded+=(_,_)=>Reflow();_resultsExpander.Collapsed+=(_,_)=>Reflow();
            Reflow();Content=workspace;
        }
        _view.SelectionChanged+=(_,_)=>RefreshGrid();var filterDelay = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        filterDelay.Tick += (_,_) => { filterDelay.Stop(); RefreshFilter(); };
        _filter.TextChanged += (_,_) => { filterDelay.Stop(); filterDelay.Start(); };
        if(initialFile!=null&&File.Exists(initialFile))_paths.Add(initialFile);
        ShowPaths();SetBusy(false);
        Status(T("Select original NAT CSV files or compare two periods. TCP checks are optional and require authorized access.","Выберите исходные CSV NAT или сравните два периода. Проверка TCP — отдельно, только при разрешённом доступе."));PanelPalette.Bind(this);SetTcpMode(false);
    }
    private static string LoadTarget(){try{var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ObserverControlCenter","collector-target.txt");return File.Exists(p)?File.ReadAllText(p).Trim():"";}catch{return "";}}
    private string[]? SelectFiles(string title)
    {
        var d=new OpenFileDialog{Title=title,Filter="CSV (*.csv)|*.csv",Multiselect=true,CheckFileExists=true,InitialDirectory=Directory.Exists(ReportLocator.ResultsRoot)?ReportLocator.ResultsRoot:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};
        return (DialogOwner is Window owner?d.ShowDialog(owner):d.ShowDialog())==true?d.FileNames:null;
    }
    private void Pick(){var selected=SelectFiles(T("Choose original NAT CSV files","Выберите исходные CSV NAT"));if(selected==null)return;_paths.Clear();_paths.AddRange(selected);ShowPaths();SetBusy(false);}
    private void ShowPaths(){_files.Text=string.Join(Environment.NewLine,_paths);if(_filesExpander!=null)_filesExpander.Header=T("Input reports: ","Исходные отчёты: ")+_paths.Count;}
    private void Status(string message){_status.Text=message;_status.Foreground=new ConsoleEntry(message,ConsoleEntry.Classify(message)).Color;LogLine?.Invoke(message);StateChanged?.Invoke();}
    private void SetBusy(bool busy)
    {
        _pick.IsEnabled=_compare.IsEnabled=_target.IsEnabled=!busy;
        _analyze.IsEnabled=!busy&&_paths.Count>0;_check.IsEnabled=_export.IsEnabled=!busy&&_analysis!=null;
        _open.IsEnabled=!busy&&_output!=null&&Directory.Exists(_output);_stop.IsEnabled=busy;StateChanged?.Invoke();
    }
    private async Task Analyze(bool compare)
    {
        if(_running!=null)return;
        string[]? earlier=null,later=null;
        if(compare)
        {
            earlier=SelectFiles(T("Choose EARLIER period CSV(s)","Выберите CSV РАННЕГО периода"));if(earlier==null)return;
            later=SelectFiles(T("Choose LATER period CSV(s)","Выберите CSV ПОЗДНЕГО периода"));if(later==null)return;
            _paths.Clear();_paths.AddRange(earlier.Concat(later).Distinct(StringComparer.OrdinalIgnoreCase));ShowPaths();
        }
        if(_paths.Count==0)return;
        string target=_target.Text;var files=_paths.ToArray();
        using var cts=new CancellationTokenSource();OperationName=compare?T("Period comparison","Сравнение периодов"):T("CSV analysis","Анализ CSV");_running=cts;SetBusy(true);Status("CHECKING CSV...");
        try
        {
            var a=await Task.Run(()=>{
                var result=AccessAnalyzer.Analyze(files,target,cts.Token);
                if(earlier!=null&&later!=null)AccessAnalyzer.Compare(result,AccessAnalyzer.Analyze(earlier,target,cts.Token),AccessAnalyzer.Analyze(later,target,cts.Token));
                return result;
            },cts.Token);
            if (_analysis != null && a.Audit.Count > 0 && a.Audit.All(row => row.Result.StartsWith("ERROR")))
                throw new InvalidDataException(a.Audit[0].Result);
            _output=null;
            _analysis=a;_view.SelectedIndex=compare&&!_tcpMode?3:0;RefreshGrid();
            Status(a.Audit.Any(r=>r.Result.StartsWith("ERROR"))?T("PARTIAL: inspect import audit; some input files failed.","PARTIAL: проверьте аудит, часть файлов не обработана."):T("COMPLETE: analysis ready. No firewall decision is made automatically.","COMPLETE: анализ готов. Решения о правилах автоматически не принимаются."));
        }
        catch(OperationCanceledException){Status("CANCELLED");}
        catch(Exception ex){Status("ERROR: "+ex.Message);}
        finally{_running=null;SetBusy(false);}
    }
    private async Task Check()
    {
        if(_analysis==null||_running!=null)return;
        if(MessageBox.Show(DialogOwner!,T("Check only observed TCP destinations in the private target subnet from this PC? Confirm authorization. No application payload is sent.","Проверить только наблюдаемые TCP-назначения в частной целевой сети с этого ПК? Подтвердите разрешение. Прикладные данные не отправляются."),T("Observer · Access analyzer","Observer · Анализ доступов"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        using var cts=new CancellationTokenSource();OperationName=T("TCP check","Проверка TCP");_running=cts;SetBusy(true);
        var progress=new Progress<string>(Status);
        try{await AccessAnalyzer.CheckTcp(_analysis,cts.Token,s=>((IProgress<string>)progress).Report(s));Status("COMPLETE: TCP checks finished; UDP remains observation only.");}
        catch(OperationCanceledException){Status("CANCELLED: completed checks retained.");}
        catch(Exception ex){Status("ERROR: "+ex.Message);}
        finally{_running=null;RefreshGrid();SetBusy(false);}
    }
    private async Task Export()
    {
        if(_analysis==null||_running!=null)return;
        var a=_analysis;
        string folder=Path.Combine(ReportLocator.ResultsRoot,"Access_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")[..8]);
        using var cts=new CancellationTokenSource();OperationName=T("Report export","Экспорт отчёта");_running=cts;SetBusy(true);
        try{var excel=await Task.Run(()=>Engine.SaveAccessReport(folder,a));_output=folder;Status(excel.Length>0?T("COMPLETE: CSV + XLSX saved.","COMPLETE: CSV и XLSX сохранены."):T("PARTIAL: CSV saved, XLSX unavailable.","PARTIAL: CSV сохранён, XLSX недоступен."));}
        catch(Exception ex){Status("ERROR: "+ex.Message);}
        finally{_running=null;SetBusy(false);}
    }
    private void RefreshGrid()
    {
        if(_grid==null)return;
        var a=_analysis;if(_empty!=null){_empty.Visibility=a==null||(_view.SelectedIndex==0&&(_tcpMode?!a.Endpoints.Any(r=>r.Protocol=="TCP"):a.Endpoints.Count==0))?Visibility.Visible:Visibility.Collapsed;_empty.Text=_tcpMode?T("Prepare endpoints from a NAT report, then check TCP.","Подготовьте назначения из отчёта NAT, затем проверьте TCP."):T("Select a NAT CSV or collect NAT in Routers, then analyze.","Выберите CSV NAT или соберите NAT в разделе «Роутеры», затем запустите анализ.");}
        if(a==null){_grid.ItemsSource=null;_summary.Text="";return;}
        _summary.Text=T("Endpoints","Назначения")+": "+a.Endpoints.Count+" | "+T("Source rules","Связи источников")+": "+a.Sources.Count+" | TCP OPEN "+a.Endpoints.Count(r=>r.TcpStatus=="TCP OPEN")+" | UDP "+a.Endpoints.Count(r=>r.Protocol=="UDP")+" | "+T("Import files","Файлы")+": "+a.Audit.Count;
        _grid.ItemsSource=_view.SelectedIndex switch
        {
            1=>a.Sources.Select(r=>new{r.Router,r.SourceIP,r.SourceName,r.SourceMAC,r.DestinationIP,r.DestinationPort,r.Protocol,r.Observations,r.FirstSeen,r.LastSeen}).ToArray(),
            2=>a.Audit.Select(r=>new{r.File,r.Result,r.Rows,r.Accepted,r.Duplicate,r.ReverseOrOutside,r.Invalid,r.Unsupported}).ToArray(),
            3=>a.Changes.Select(r=>new{r.IP,r.Port,r.Protocol,r.Earlier,r.Later,r.Change}).ToArray(),
            4=>a.SourceChanges.Select(r=>new{r.Router,r.SourceIP,r.DestinationIP,r.DestinationPort,r.Protocol,r.Earlier,r.Later,r.Change}).ToArray(),
            _=>a.Endpoints.Where(r=>!_tcpMode||r.Protocol=="TCP").Select(r=>new{r.IP,r.Port,r.Protocol,r.Observations,r.Sources,r.Routers,TcpStatus=TcpLabel(r.TcpStatus),r.ServiceHint,r.Review,r.CheckedAt,r.FirstSeen,r.LastSeen,r.CheckDetail}).ToArray()
        };
        RefreshFilter();
    }
    private static string TcpLabel(string status) => status switch
    {
        "TCP OPEN" => T("Open","Открыт"),
        "TCP REFUSED" => T("Refused","Отказ"),
        "TCP TIMEOUT" => T("Timeout","Тайм-аут"),
        "TCP ERROR" => T("Connection error","Ошибка подключения"),
        "NOT CHECKED" => T("Not checked","Не проверено"),
        "UDP OBSERVED" => T("UDP observation","Наблюдение UDP"),
        _ => status
    };

    private void RefreshFilter()
    {
        if (_grid?.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(_grid.ItemsSource);
        var query = _filter.Text.Trim();
        var type = _grid.ItemsSource.Cast<object>().FirstOrDefault()?.GetType();
        var properties = type?.GetProperties() ?? [];
        view.Filter = query.Length == 0 ? null : row => properties.Any(p =>
            p.GetValue(row)?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }
}

internal sealed class AccessAnalyzerWindow : Window
{
    private readonly AccessAnalyzerPanel _panel;
    public AccessAnalyzerWindow(string? initialFile=null,string? target=null)
    {
        Title=UiText.T("Observer · Access analyzer","Observer · Анализ сетевых доступов");Width=1150;Height=780;MinWidth=720;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterOwner;DarkTitleBar.Apply(this);
        SetResourceReference(BackgroundProperty,"DialogBg");SetResourceReference(ForegroundProperty,"DialogText");
        _panel=new AccessAnalyzerPanel(initialFile,target);Content=_panel;
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e){if(_panel.IsBusy){_panel.Stop();e.Cancel=true;}base.OnClosing(e);}
}
