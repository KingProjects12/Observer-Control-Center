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
    private DataGrid? _issuesGrid;
    private StackPanel? _issuesPanel;
    private Button? _retryIssues;
    private string _module="routers";
    private readonly DispatcherTimer _moduleTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    private DateTime? _moduleStarted;
    private string _lastModuleLog="";
    private static string T(string en,string ru)=>UiText.T(en,ru);
    private void InitializeModules()
    {
        NavOverview.Content=T("⌂  Workspace","⌂  Рабочая область");
        foreach(var (id,icon,en,ru,subEn,subRu) in new[]{
            ("routers","▣","Routers","Роутеры","NAT / VPN REMOTE","NAT / VPN REMOTE"),
            ("connections","⌘","Connections","Соединения","Access analysis / CSV","Анализ доступов / CSV"),
            ("tcp","◇","TCP","TCP","Observed endpoints","Доступность портов"),
            ("traffic","▥","Traffic","Трафик","TOP Traffic","ТОП трафика"),
            ("issues","△","Issues","Проблемы","Errors / retry","Ошибки / повтор")})
        {
            var b=new Button{Style=(Style)FindResource("ModuleTileButton"),Margin=new Thickness(0,0,8,0),Padding=new Thickness(12,9,12,9),MinHeight=76,ToolTip=T(subEn,subRu)};
            b.SetResourceReference(Control.BackgroundProperty,"Module_"+id+"_Bg");
            var content=new StackPanel();
            var title=new TextBlock{Text=icon+"  "+T(en,ru),FontSize=16,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};title.SetResourceReference(TextBlock.ForegroundProperty,"Module_"+id+"_Fg");content.Children.Add(title);
            var hint=new TextBlock{Text=T(subEn,subRu),FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,5,0,0)};hint.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");content.Children.Add(hint);
            b.Content=content;b.Click+=(_,_)=>ShowModule(id);ModuleTiles.Children.Add(b);_moduleButtons.Add(id,b);
        }
        _collector=new LegacyCollectorPanel(RouterIP.Text,vpn:true,embedded:true,offline:_offlineUiCheck);
        _analyzer=new AccessAnalyzerPanel();
        _collector.LogLine+=ModuleLog;_analyzer.LogLine+=ModuleLog;
        _collector.StateChanged+=UpdateModuleSummary;_analyzer.StateChanged+=UpdateModuleSummary;
        _collector.NatReady+=(file,target)=>{_reportPath=file;_analyzer.SetInput(file,target);UpdateModuleSummary();};
        _collector.NavigateAccess+=()=>ShowModule("connections");
        _collector.CsvSelected+=path=>{if(Path.GetFileName(path).StartsWith("08_Top_Traffic",StringComparison.OrdinalIgnoreCase))LoadTraffic(path);else{LoadConnections(path);ShowPage("connections");}_reportPath=path;};
        _issuesPanel=new StackPanel{Margin=new Thickness(12)};
        _issuesPanel.Children.Add(new TextBlock{Text=T("Issues and incomplete routers","Проблемы и незавершённые роутеры"),FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
        _retryIssues=new Button{Content=T("Retry incomplete routers","Повторить проблемные"),Style=(Style)FindResource("AccentButton"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8),IsEnabled=false};
        _retryIssues.Click+=async (_,_)=>{ShowModule("routers");await _collector.RetryAsync();};_issuesPanel.Children.Add(_retryIssues);
        _issuesGrid=new DataGrid{AutoGenerateColumns=true,IsReadOnly=true,CanUserAddRows=false,MinHeight=110,MaxHeight=280};_issuesPanel.Children.Add(_issuesGrid);
        _issuesPanel.Children.Add(new TextBlock{Text=T("See the shared log below for authentication, API and timeout details.","Подробности авторизации, API и тайм-аутов — в общем журнале ниже."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
        ModuleStop.Content=T("Stop","Стоп");
        _moduleTimer.Tick+=(_,_)=>UpdateModuleSummary();_moduleTimer.Start();
        UpdateModuleSummary();
    }
    private void ModuleLog(string message){_lastModuleLog=message;AddLog(message);UpdateModuleSummary();}
    private void ShowModule(string id)
    {
        _page="overview";DisplayModule(id);RefreshNavigationSelection();
    }
    private void DisplayModule(string id)
    {
        if(_collector==null||_analyzer==null)return;
        // Switching surfaces never destroys a session or opens a modal window.
        _module=id;ModuleSurface.Visibility=Visibility.Visible;OldKpiPanel.Visibility=OldReportPanel.Visibility=OldConnectionHeader.Visibility=Visibility.Collapsed;
        foreach(var pair in _moduleButtons)pair.Value.Tag=pair.Key==id?"selected":null;
        if(id=="routers"||id=="traffic")
        {
            _collector.SetMode(id=="traffic"?"traffic":"nat");
            ModuleHost.Content=_collector;
        }
        else if(id=="issues")ModuleHost.Content=_issuesPanel;
        else {_analyzer.SetTcpMode(id=="tcp");ModuleHost.Content=_analyzer;}
        // Keep collectors mutually exclusive; switching for review is allowed during a run.
        UpdateModuleSummary();
        if(IsLoaded&&SystemParameters.ClientAreaAnimation)
        {
            ModuleSurface.BeginAnimation(OpacityProperty,new DoubleAnimation(.82,1,TimeSpan.FromMilliseconds(150)){FillBehavior=FillBehavior.Stop});
        }
    }
    private void DisplayLegacyReport()
    {
        if(_collector==null)return;
        ModuleSurface.Visibility=Visibility.Collapsed;OldReportPanel.Visibility=OldKpiPanel.Visibility=OldConnectionHeader.Visibility=Visibility.Visible;
        foreach(var button in _moduleButtons.Values)button.Tag=null;
    }
    private void UpdateModuleSummary()
    {
        if(_collector==null||_analyzer==null)return;
        bool collect=_collector.IsBusy, analyze=_analyzer.IsBusy,busy=collect||analyze;
        // Avoid concurrent sessions mutating the original collector's shared target.
        _collector.IsEnabled=!analyze;_analyzer.IsEnabled=!collect;
        if(busy&&_moduleStarted==null)_moduleStarted=DateTime.Now;
        if(!busy)_moduleStarted=null;
        ModuleStop.IsEnabled=busy;ModuleProgress.IsIndeterminate=analyze||(collect&&_collector.RouterCount==0);
        ModuleProgress.Maximum=Math.Max(1,_collector.RouterCount);ModuleProgress.Value=_collector.Processed;
        ModuleSummary.Text=T("Routers ","Роутеры ")+_collector.Processed+"/"+_collector.RouterCount+"   •   "+T("Office observations ","Наблюдений в офис ")+_collector.OfficeCount+"   •   TCP OPEN "+(_analyzer.OpenTcp?.ToString()??"—")+"   •   "+T("Failed / partial ","Ошибки / частично ")+_collector.FailedCount+"/"+_collector.PartialCount;
        ModuleProgressText.Text=(busy?T("Running","Выполнение")+"  "+(DateTime.Now-_moduleStarted!.Value).ToString(@"hh\:mm\:ss")+"  •  ":"")+_lastModuleLog;
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
        foreach(string id in new[]{"routers","traffic","connections","tcp","issues","routers"})
        {
            ShowModule(id);
            if(!Equals(_moduleButtons[id].Tag,"selected")||ModuleHost.Content==null||ModuleSurface.Visibility!=Visibility.Visible)return false;
            if((id=="routers"||id=="traffic")&&!ReferenceEquals(ModuleHost.Content,_collector))return false;
            if((id=="connections"||id=="tcp")&&!ReferenceEquals(ModuleHost.Content,_analyzer))return false;
        }
        return !ModuleStop.IsEnabled&&_moduleButtons.Count==5;
    }
}
