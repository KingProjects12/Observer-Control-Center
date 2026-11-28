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
    private static void usingThemeGallery(MainWindow main,object? before)
    {
        bool snow=AppearanceTheme.SnowEnabled;
        var gallery=new ThemeSettingsWindow(id=>AppearanceTheme.Apply(id),enabled=>AppearanceTheme.SetSnow(enabled));
        try
        {
            Require(gallery.CardCount==26,"all theme cards");
            Require(ThemeCatalog.All.Select(p=>p.Id).Distinct().Count()==26,"unique theme ids");
            gallery.SetFilter(1,"");Require(gallery.VisibleCardCount==2,"core themes filter");
            gallery.SetFilter(2,"");Require(gallery.VisibleCardCount==6,"studio filter");
            gallery.SetFilter(3,"");Require(gallery.VisibleCardCount==10,"worlds filter");
            gallery.SetFilter(4,"");Require(gallery.VisibleCardCount==8,"lab filter");
            gallery.SetFilter(0,"Сакура");Require(gallery.VisibleCardCount==1,"Russian search");
            gallery.SetFilter(0,"Tokyo");Require(gallery.VisibleCardCount==1,"English search");
            gallery.SetFilter(0,"no-such-theme");Require(gallery.VisibleCardCount==0,"empty search");
            gallery.SetFilter(0,"");Require(gallery.VisibleCardCount==26,"reset filters");
            foreach(var preset in ThemeCatalog.All)
            {
                gallery.Choose(preset.Id);Layout(gallery);Layout(main);
                Require(main.ModuleSmoke(), "navigation, status sorting and selection: " + preset.Id);
                Require(AppearanceTheme.Current.Id==preset.Id,"theme selection "+preset.Id);
                Require(((SolidColorBrush)main.FindResource("Accent")).Color==(Color)ColorConverter.ConvertFromString(preset.Accent)!,"theme accent "+preset.Id);
                Require(ReferenceEquals(before,((DataGrid)main.FindName("TrafficGrid")).ItemsSource),"theme keeps data");
                if(preset.Collection!="original")Require(main.FindResource("HeaderGradient") is DrawingBrush,"live theme artwork "+preset.Id);
            }
            AppearanceTheme.SetSnow(false);Require(!AppearanceTheme.SnowEnabled,"snow disabled");
            AppearanceTheme.SetSnow(true);Require(!ThemeCatalog.All.Any(p=>p.HasSnow),"no seasonal themes in current gallery");
            foreach(string removedId in new[]{"spring","summer","autumn","winter","newyear","midnight","graphite","ocean","forest","sunset","rose","lavender","aurora","nebula","ember","arctic"})
            {
                Require(!ThemeCatalog.All.Any(p=>p.Id==removedId),"removed theme "+removedId);
                Require(ThemeCatalog.Get(removedId).Id=="classic","removed preference fallback "+removedId);
            }
            Require(gallery.FindName("EffectsTab") is TabItem effects&&effects.Visibility==Visibility.Collapsed,"seasonal effects hidden");
            AppearanceTheme.Apply("winter");Require(AppearanceTheme.Current.Id=="classic","old theme safely restored to core");
        }
        finally {gallery.Close();AppearanceTheme.SetSnow(snow);AppearanceTheme.Apply(true);}
    }
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
                Require(banner.Visibility==Visibility.Visible,"classic banner and spacing");
                var before=((DataGrid)main.FindName("TrafficGrid")).ItemsSource;
                Click(FindButton(main,"ThemeSignalButton"));
                foreach(var w in windows)Layout(w);
                Require(AppearanceTheme.IsSignal && banner.Visibility==Visibility.Visible,"signal theme and spacing");
                Require((FindButton(main,"ThemeSignalButton").Tag as string)=="selected","signal selection");
                Require(ReferenceEquals(before,((DataGrid)main.FindName("TrafficGrid")).ItemsSource),"theme preserves data binding");
                Require(((SolidColorBrush)main.FindResource("Accent")).Color==Color.FromRgb(105,223,255),"signal accent");
                Click(FindButton(main,"ThemeClassicButton"));Layout(main);
                Require(!AppearanceTheme.IsSignal && banner.Visibility==Visibility.Visible,"classic restored");
                Require(((SolidColorBrush)main.FindResource("Accent")).Color==Color.FromRgb(61,231,234),"classic accent restored");
                Click(FindButton(main,"ThemeClassicButton"));
                Require(main.ModuleSmoke(),"five embedded modules / shared state classic");
                Click(FindButton(main,"ThemeSignalButton"));
                Require(main.ModuleSmoke(),"five embedded modules / shared state signal");
                Require(main.FindResource("WorkspaceSection") is Style,"shared section style");
                Require(main.FindResource("CardGradient") is SolidColorBrush,"Signal surfaces are flat");
                usingThemeGallery(main,before);
                Require(main.ConsoleSmoke(), "three log columns and early warning retained after 350 events");
                var island=windows.OfType<MiniIslandWindow>().Single();
                island.SetOperation("NAT","12/39",true);
                island.SetUpdateAvailable("vNext");
                Require(((TextBlock)island.FindName("CountText")).Text=="12/39","active operation precedes update");
                island.SetOperation("NAT","COMPLETE",false);
                island.ClearUpdateNotice();
                Require(((TextBlock)island.FindName("CountText")).Text=="COMPLETE","result survives update notice");
                Click(FindButton(main,"ThemeClassicButton"));
            }
            finally { foreach(var w in windows.Reverse()) w.Close(); }
        }
    }
}
