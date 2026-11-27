using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Observer.ControlCenter.Services;
using Observer.ControlCenter.Models;
namespace Observer.ControlCenter;
public partial class MainWindow
{
    private LegacyCollectorPanel? _collector;
    private AccessAnalyzerPanel? _analyzer;
    private readonly Dictionary<string,Button> _moduleButtons=new();
    private readonly Dictionary<string,string> _moduleTitles=new();
    private readonly TextBlock[] _sessionValues=new TextBlock[4];
    private DataGrid? _issuesGrid;
    private StackPanel? _issuesPanel;
    private Button? _retryIssues;
    private string _module="";
    private string? _trafficReport,_networkReport;
    private bool _tableExpanded=true,_logExpanded=true;
    private readonly DispatcherTimer _moduleTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    private DateTime? _moduleStarted;
    private string _lastModuleLog="";
    private static string T(string en,string ru)=>UiText.T(en,ru);
    private static System.Windows.Shapes.Path Glyph(string geometry,string brushKey,double size=32)
    {
        var p=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Width=size,Height=size,Stretch=Stretch.Uniform,StrokeThickness=2,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,VerticalAlignment=VerticalAlignment.Center};
        p.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty,brushKey);return p;
    }
    private void InitializeModules()
    {
        BtnSettings.ToolTip=T("Application settings","Настройки приложения");
        DashboardTagline.Text=T("Unified network data collection and analytics.","Единый центр сбора и анализа сетевых данных.");
        DashboardExport.Content=T("↓ Export CSV","↓ Экспорт CSV");
        ClearDashboardLog.Content=T("Clear","Очистить");
        LogCount.Text=_consoleItems.Count+" "+T("events","событий");
        NavOverview.Content=T("Home","Главная");
        var definitions=new[]{
            ("routers","M2,16 L2,25 L30,25 L30,16 Z M7,16 L7,5 M25,16 L25,5 M5,4 A2,2 0 1 0 9,4 A2,2 0 1 0 5,4 M23,4 A2,2 0 1 0 27,4 A2,2 0 1 0 23,4 M7,21 L9,21 M13,21 L15,21 M19,21 L21,21","Routers","Роутеры","Collection and analysis","Сбор и анализ устройств"),
            ("connections","M16,7 L6,24 L26,24 Z M12,5 A4,4 0 1 0 20,5 A4,4 0 1 0 12,5 M2,25 A4,4 0 1 0 10,25 A4,4 0 1 0 2,25 M22,25 A4,4 0 1 0 30,25 A4,4 0 1 0 22,25","Connections","Соединения","NAT and access","Доступы, NAT и маршруты"),
            ("tcp","M16,2 L28,7 L26,20 Q23,28 16,31 Q9,28 6,20 L4,7 Z M16,11 L16,20 M12,16 L20,16","TCP","TCP","Observed port checks","Проверка портов и сервисов"),
            ("traffic","M4,27 L4,18 L9,18 L9,27 Z M13,27 L13,11 L18,11 L18,27 Z M22,27 L22,3 L27,3 L27,27 Z","Traffic","Трафик","TOP Traffic","Анализ и статистика"),
            ("issues","M16,3 L30,28 L2,28 Z M16,12 L16,19 M16,23 L16,24","Issues","Проблемы","Errors and timeouts","Ошибки и предупреждения")};
        foreach(var (id,geometry,en,ru,subEn,subRu) in definitions)
        {
            var b=new Button{Style=(Style)FindResource("ModuleTileButton"),Margin=new Thickness(0,0,id=="issues"?0:9,0),Padding=new Thickness(14,10,14,10),MinHeight=82,ToolTip=T(subEn,subRu)};
            b.SetResourceReference(Control.BackgroundProperty,"Module_"+id+"_Bg");b.SetResourceReference(Control.BorderBrushProperty,"Module_"+id+"_Fg");
            var row=new DockPanel{LastChildFill=true};var icon=Glyph(geometry,"Module_"+id+"_Fg",36);icon.Margin=new Thickness(0,0,12,0);DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
            var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            var title=new TextBlock{Text=T(en,ru),FontSize=17,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};title.SetResourceReference(TextBlock.ForegroundProperty,"TextMain");content.Children.Add(title);
            var hint=new TextBlock{Text=T(subEn,subRu),FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,4,0,0)};hint.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");content.Children.Add(hint);row.Children.Add(content);
            var arrow=new TextBlock{Text="›",FontSize=25,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)};
            arrow.SetResourceReference(TextBlock.ForegroundProperty,"Module_"+id+"_Fg");DockPanel.SetDock(arrow,Dock.Right);row.Children.Insert(1,arrow);
            b.Content=row;b.Click+=(_,_)=>{if(_module==id)ShowPage("overview");else ShowModule(id);};ModuleTiles.Children.Add(b);_moduleButtons.Add(id,b);_moduleTitles[id]=T(en,ru);
        }
        SessionTitle.Text=T("Session results","Результаты сессии");SessionHint.Text=T("Actual observations","Фактические наблюдения");
        var metrics=new[]{("routers",T("Routers","Роутеров")),("connections",T("Office observations","Наблюдений в офис")),("tcp",T("Reachable TCP","Доступных TCP")),("issues",T("Failed / partial","Ошибки / частично"))};
        for(int i=0;i<metrics.Length;i++)
        {
            var (id,label)=metrics[i];var panel=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            panel.Children.Add(new TextBlock{Text=label,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis});
            var value=new TextBlock{Text="—",FontSize=25,FontWeight=FontWeights.Bold,Margin=new Thickness(0,3,0,0)};value.SetResourceReference(TextBlock.ForegroundProperty,"Module_"+id+"_Fg");panel.Children.Add(value);_sessionValues[i]=value;
            var metric=new DockPanel{Margin=new Thickness(6,2,5,2)};
            var icon=Glyph(definitions.First(d=>d.Item1==id).Item2,"Module_"+id+"_Fg",29);icon.Margin=new Thickness(0,0,10,0);DockPanel.SetDock(icon,Dock.Left);metric.Children.Add(icon);metric.Children.Add(panel);SessionMetrics.Children.Add(metric);
        }
        TrafficStatusColumn.Header=T("Status","Статус");
        _collector=new LegacyCollectorPanel(RouterIP.Text,vpn:true,embedded:true,offline:_offlineUiCheck);
        _analyzer=new AccessAnalyzerPanel();
        _collector.LogLine+=ModuleLog;_analyzer.LogLine+=ModuleLog;
        _collector.StateChanged+=UpdateModuleSummary;_analyzer.StateChanged+=UpdateModuleSummary;
        _collector.NatReady+=(file,target)=>{_reportPath=file;_analyzer.SetInput(file,target);UpdateModuleSummary();};
        _collector.ReportReady+=(file,traffic)=>{if(traffic)LoadTraffic(file,false);else LoadConnections(file);UpdateModuleSummary();};
        _collector.NavigateAccess+=()=>ShowModule("connections");
        _collector.CsvSelected+=path=>{if(Path.GetFileName(path).StartsWith("08_Top_Traffic",StringComparison.OrdinalIgnoreCase))LoadTraffic(path);else{LoadConnections(path);ShowPage("connections");}_reportPath=path;};
        _issuesPanel=new StackPanel{Margin=new Thickness(12)};
        _issuesPanel.Children.Add(new TextBlock{Text=T("Issues and incomplete routers","Проблемы и незавершённые роутеры"),FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
        _retryIssues=new Button{Content=T("Retry incomplete routers","Повторить проблемные"),Style=(Style)FindResource("AccentButton"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8),IsEnabled=false};
        _retryIssues.Click+=async (_,_)=>{ShowModule("routers");await _collector.RetryAsync();};_issuesPanel.Children.Add(_retryIssues);
        _issuesGrid=new DataGrid{AutoGenerateColumns=true,IsReadOnly=true,CanUserAddRows=false,MinHeight=110,MaxHeight=280};_issuesPanel.Children.Add(_issuesGrid);
        _issuesPanel.Children.Add(new TextBlock{Text=T("See the shared log for authentication, API and timeout details.","Подробности авторизации, API и тайм-аутов — в журнале ниже."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
        ModuleStop.Content=T("Stop","Стоп");ModuleCollapse.Content=T("⌃  Home / collapse","⌃  Свернуть / главная");
        _moduleTimer.Tick+=(_,_)=>UpdateModuleSummary();_moduleTimer.Start();UpdateModuleSummary();
    }
    private void ModuleLog(string message){_lastModuleLog=message;AddLog(message);UpdateModuleSummary();}
    private void ShowModule(string id){_page="overview";DisplayModule(id);RefreshNavigationSelection();}
    private void DisplayModule(string id)
    {
        if(_collector==null||_analyzer==null)return;
        _module=id;ModuleSurface.Visibility=Visibility.Visible;OldKpiPanel.Visibility=OldReportPanel.Visibility=OldConnectionHeader.Visibility=Visibility.Collapsed;
        LogPanel.Visibility=OperationPanel.Visibility=Visibility.Visible;
        ModuleHeading.Text=_moduleTitles[id];
        LogList.Visibility=_logExpanded?Visibility.Visible:Visibility.Collapsed;
        foreach(var pair in _moduleButtons)pair.Value.Tag=pair.Key==id?"selected":null;
        if(id=="routers"||id=="traffic"){_collector.SetMode(id=="traffic"?"traffic":"nat");ModuleHost.Content=_collector;}
        else if(id=="issues")ModuleHost.Content=_issuesPanel;
        else{_analyzer.SetTcpMode(id=="tcp");ModuleHost.Content=_analyzer;}
        UpdateModuleSummary();
        if(IsLoaded&&SystemParameters.ClientAreaAnimation)ModuleSurface.BeginAnimation(OpacityProperty,new DoubleAnimation(.82,1,TimeSpan.FromMilliseconds(150)){FillBehavior=FillBehavior.Stop});
    }
    private void DisplayLegacyReport()
    {
        if(_collector==null)return;
        _module="";ModuleSurface.Visibility=OperationPanel.Visibility=OldKpiPanel.Visibility=OldConnectionHeader.Visibility=Visibility.Collapsed;
        OldReportPanel.Visibility=LogPanel.Visibility=Visibility.Visible;
        LogList.Visibility=_logExpanded?Visibility.Visible:Visibility.Collapsed;
        foreach(var button in _moduleButtons.Values)button.Tag=null;
        UpdateDashboardTableVisibility();
    }
    private void UpdateDashboardTableVisibility()
    {
        if(_module!="")return;
        TrafficGrid.Visibility=_tableExpanded&&_page!="connections"?Visibility.Visible:Visibility.Collapsed;
        ConnectionsGrid.Visibility=_tableExpanded&&_page=="connections"?Visibility.Visible:Visibility.Collapsed;
        DashboardCollapse.Content=_tableExpanded?"⌃":"⌄";
    }
    private void ModuleCollapse_Click(object sender,RoutedEventArgs e)=>ShowPage("overview");
    private void DashboardCollapse_Click(object sender,RoutedEventArgs e){_tableExpanded=!_tableExpanded;UpdateDashboardTableVisibility();}
    private void SessionToggle_Click(object sender,RoutedEventArgs e){SessionMetrics.Visibility=SessionMetrics.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;SessionToggle.Content=SessionMetrics.Visibility==Visibility.Visible?"⌃":"⌄";}
    private void LogToggle_Click(object sender,RoutedEventArgs e){_logExpanded=!_logExpanded;LogList.Visibility=_logExpanded?Visibility.Visible:Visibility.Collapsed;LogToggle.Content=_logExpanded?"⌃":"⌄";}
    private void DashboardTableToggle_Click(object sender,RoutedEventArgs e){_tableExpanded=true;if(_page=="connections")ShowPage("overview");else Connections_Click(sender,e);}
    private void DashboardExport_Click(object sender,RoutedEventArgs e)=>ExportDashboard();
    private void ClearDashboardLog_Click(object sender,RoutedEventArgs e){_consoleItems.Clear();LogList.Document.Blocks.Clear();LogCount.Text="0 "+T("events","событий");}
    private void DashboardActions_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=(UIElement)sender,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom};
        void Item(string label,RoutedEventHandler handler){var item=new MenuItem{Header=label};item.Click+=handler;menu.Items.Add(item);}
        Item(T("Import CSV","Импорт CSV"),Import_Click);
        Item(T("Export displayed report CSV","Экспорт текущего CSV"),(_,_)=>ExportDashboard());
        Item(T("Open report folder","Открыть папку отчётов"),Reports_Click);
        menu.IsOpen=true;
    }
    private void ExportDashboard()
    {
        var file=_page=="connections"?_networkReport:_trafficReport;
        if(file==null||!File.Exists(file)){MessageBox.Show(this,T("No report loaded.","Отчёт не загружен."));return;}
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName=Path.GetFileName(file)};
        if(dialog.ShowDialog(this)!=true)return;
        try{if(!string.Equals(Path.GetFullPath(file),Path.GetFullPath(dialog.FileName),StringComparison.OrdinalIgnoreCase))File.Copy(file,dialog.FileName,true);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){MessageBox.Show(this,ex.Message);}
    }
    private void SettingsMenu_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=(UIElement)sender,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom};
        void Item(string label,RoutedEventHandler handler){var item=new MenuItem{Header=label};item.Click+=handler;menu.Items.Add(item);}
        var themes=new MenuItem{Header=T("Appearance","Оформление")};menu.Items.Add(themes);
        foreach(bool signal in new[]{true,false}){var theme=new MenuItem{Header=signal?"Signal":"Black Liquid Glass",IsCheckable=true,IsChecked=AppearanceTheme.IsSignal==signal};theme.Click+=(_,_)=>ChangeAppearance(signal);themes.Items.Add(theme);}
        Item(T("Change accent","Сменить акцент"),Theme_Click);Item(T("Glass opacity","Прозрачность стекла"),Glass_Click);
        menu.Items.Add(new Separator());
        Item(T("Home","Главная"),Overview_Click);Item(T("Import CSV","Импорт CSV"),Import_Click);Item(T("Reports","Отчёты"),Reports_Click);
        Item(T("Check router TCP 80/443","Проверка TCP роутера 80/443"),RouterCheck_Click);
        Item(T("Updates / language","Обновления / язык"),Updates_Click);Item(T("Help / F1","Справка / F1"),Help_Click);
        menu.IsOpen=true;
    }
    private void UpdateModuleSummary()
    {
        if(_collector==null||_analyzer==null)return;
        bool collect=_collector.IsBusy,analyze=_analyzer.IsBusy,busy=collect||analyze;
        _collector.IsEnabled=!analyze;_analyzer.IsEnabled=!collect;
        if(busy&&_moduleStarted==null)_moduleStarted=DateTime.Now;
        if(!busy)_moduleStarted=null;
        ModuleStop.IsEnabled=busy;ModuleProgress.IsIndeterminate=analyze||(collect&&(!_collector.IsNatRunning||_collector.RouterCount==0));
        ModuleProgress.Maximum=Math.Max(1,_collector.RouterCount);ModuleProgress.Value=_collector.Processed;
        _sessionValues[0].Text=_collector.RouterCount==0?"—":_collector.Processed+"/"+_collector.RouterCount;
        _sessionValues[1].Text=_collector.RouterCount==0?"—":_collector.OfficeCount.ToString();
        _sessionValues[2].Text=_analyzer.OpenTcp?.ToString()??"—";
        _sessionValues[3].Text=_collector.RouterCount==0?"—":_collector.FailedCount+"/"+_collector.PartialCount;
        ModuleSummary.Text=busy?T("Operation running","Выполняется операция"):T("Ready","Готово");
        ModuleProgressText.Text=(busy?(DateTime.Now-_moduleStarted!.Value).ToString(@"hh\:mm\:ss")+"  •  ":"")+_lastModuleLog;
        if(_issuesGrid!=null)_issuesGrid.ItemsSource=_collector.Issues;
        if(_retryIssues!=null)_retryIssues.IsEnabled=_collector.CanRetry&&!analyze;
    }
    private void ModuleStop_Click(object sender,RoutedEventArgs e){_collector?.Stop();_analyzer?.Stop();}
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if(_collector?.IsBusy==true||_analyzer?.IsBusy==true){ModuleStop_Click(this,new RoutedEventArgs());e.Cancel=true;AddLog(T("STOP: operation is finishing; close again when idle.","СТОП: завершаю операцию; закройте окно после остановки."));}
        base.OnClosing(e);
    }
    internal bool ModuleSmoke()
    {
        if(_collector==null||_analyzer==null)return false;
        ShowPage("overview");
        if(LogPanel.Visibility!=Visibility.Visible||OldReportPanel.Visibility!=Visibility.Visible||RetiredSidebar.Visibility!=Visibility.Collapsed)return false;
        bool expanded=_logExpanded;LogToggle_Click(this,new RoutedEventArgs());
        if(LogList.Visibility!=(expanded?Visibility.Collapsed:Visibility.Visible))return false;
        LogToggle_Click(this,new RoutedEventArgs());
        foreach(string id in new[]{"routers","traffic","connections","tcp","issues","routers"})
        {
            ShowModule(id);
            if(!Equals(_moduleButtons[id].Tag,"selected")||ModuleHost.Content==null||ModuleSurface.Visibility!=Visibility.Visible||LogPanel.Visibility!=Visibility.Visible)return false;
            if((id=="routers"||id=="traffic")&&!ReferenceEquals(ModuleHost.Content,_collector))return false;
            if((id=="connections"||id=="tcp")&&!ReferenceEquals(ModuleHost.Content,_analyzer))return false;
        }
        ModuleCollapse_Click(this,new RoutedEventArgs());
        return !ModuleStop.IsEnabled&&_moduleButtons.Count==5&&LogPanel.Visibility==Visibility.Visible&&OldReportPanel.Visibility==Visibility.Visible;
    }
}
