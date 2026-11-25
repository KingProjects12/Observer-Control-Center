using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

internal static class UiSmoke
{
    private static void Require(bool ok,string description)
    { if(!ok) throw new InvalidOperationException("UI smoke: "+description); }
    private static void Layout(Window w)
    { w.ApplyTemplate(); w.Measure(new Size(w.Width,w.Height)); w.Arrange(new Rect(0,0,w.Width,w.Height)); w.UpdateLayout(); }
    private static Button FindButton(Window w,string name) =>
        w.FindName(name) as Button ?? throw new InvalidOperationException("Missing "+name);
    private static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,b));
    public static void Run()
    {
        foreach(string language in new[]{"english","russian"})
        {
            UiText.ApplyLanguage(language);
            Require(UiText.IsRussian==(language=="russian"),"language");
            AppearanceTheme.Apply(false);
            var main=new MainWindow(true);
            var update=new UpdateCenterWindow(main);
            Window[] windows=[main,update,new LegacyCollectorWindow(""),new LegacyCollectorWindow("",vpn:true),new LegacyCollectorWindow("",traffic:true),new WorkspaceGuideWindow(),new AccessAnalyzerWindow(),new MiniIslandWindow(main),new UpdateNoticeWindow("v0.0.0",()=>{})];
            try
            {
                foreach(var w in windows) Layout(w);
                Require((FindButton(main,"NavOverview").Tag as string)=="selected","overview selection");
                Click(FindButton(main,"NavTraffic"));
                Require((FindButton(main,"NavTraffic").Tag as string)=="selected","traffic click");
                Click(FindButton(main,"NavOverview"));
                Require((FindButton(main,"NavOverview").Tag as string)=="selected","overview click");
                Require(FindButton(main,"NavConnections").IsEnabled,"connections enabled");
                Require(FindButton(main,"NavHelp").IsEnabled,"help enabled");
                Require(FindButton(update,"CheckButton").IsEnabled,"check enabled");
                Require(FindButton(update,"InstallButton").Visibility==Visibility.Collapsed,"no release means no install");
                var save=FindButton(update,"SaveButton"); save.IsEnabled=false; save.ApplyTemplate();
                var surface=save.Template.FindName("BackgroundBorder",save) as Border;
                Require(surface?.Background is SolidColorBrush b && b.Color.R<64 && b.Color.G<64 && b.Color.B<64,"disabled button is dark");
                var progress=update.FindName("InstallProgress") as ProgressBar;
                Require(progress!=null,"progress exists"); progress!.Visibility=Visibility.Visible; progress.Value=93;
                Layout(update); Require(progress.Value==93,"progress");
                var nav=FindButton(main,"NavOverview");
                var banner=(Border)main.FindName("SignalBanner");
                Require(banner.Visibility==Visibility.Collapsed && nav.Margin.Bottom==5,"classic theme");
                var before=((DataGrid)main.FindName("TrafficGrid")).ItemsSource;
                Click(FindButton(main,"ThemeSignalButton"));
                foreach(var w in windows)Layout(w);
                Require(AppearanceTheme.IsSignal && banner.Visibility==Visibility.Visible && nav.Margin.Bottom==9,"signal theme and spacing");
                Require((FindButton(main,"ThemeSignalButton").Tag as string)=="selected","signal selection");
                Require(ReferenceEquals(before,((DataGrid)main.FindName("TrafficGrid")).ItemsSource),"theme preserves data binding");
                Require(((SolidColorBrush)main.FindResource("Accent")).Color==Color.FromRgb(105,223,255),"signal accent");
                Click(FindButton(main,"ThemeClassicButton"));Layout(main);
                Require(!AppearanceTheme.IsSignal && banner.Visibility==Visibility.Collapsed && nav.Margin.Bottom==5,"classic restored");
                Require(((SolidColorBrush)main.FindResource("Accent")).Color==Color.FromRgb(61,231,234),"classic accent restored");
            }
            finally { foreach(var w in windows.Reverse()) w.Close(); }
        }
    }
}
