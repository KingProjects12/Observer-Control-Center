using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Observer.ControlCenter.Services;

// Presentation resources and a non-sensitive per-user preference only.
internal static class AppearanceTheme
{
    private const string RegistryPath = @"Software\KingProjects12\ObserverControlCenter";
    private static ResourceDictionary? _palette;
    internal static bool IsSignal { get; private set; }
    private static readonly (string Key,string Classic,string Signal)[] Colors =
    [
        ("WindowBg", "#FD050608", "#FF0B101A"),
        ("PanelBg", "#ED080A0D", "#FF121B2B"),
        ("PanelBg2", "#F00F1216", "#FF121B2B"),
        ("Stroke", "#273A49", "#29394E"),
        ("Accent", "#3DE7EA", "#69DFFF"),
        ("Accent2", "#B392FF", "#B3A0FF"),
        ("TextMain", "#EDF5FB", "#F0F5FF"),
        ("TextSecondary", "#92A5B5", "#A0AEC3"),
        ("Positive", "#3BD5AA", "#3BD5AA"),
        ("DialogBg", "#07090C", "#0B101A"),
        ("DialogText", "#FFFFFF", "#F0F5FF"),
        ("DialogAccent", "#40E0D0", "#69DFFF"),
        ("DialogField", "#0C0F12", "#121B2B"),
        ("DialogFieldStroke", "#2F4F4F", "#40536C"),
        ("DialogLog", "#000000", "#0B101A"),
        ("Tone_06090F", "#06090F", "#0B101A"),
        ("Tone_070809", "#070809", "#0B101A"),
        ("Tone_07090B", "#07090B", "#0B101A"),
        ("Tone_080C12", "#080C12", "#0B101A"),
        ("Tone_090B0E", "#090B0E", "#0B101A"),
        ("Tone_090C10", "#090C10", "#0B101A"),
        ("Tone_0A1015", "#0A1015", "#121B2B"),
        ("Tone_0B0D10", "#0B0D10", "#0B101A"),
        ("Tone_0B0E15", "#0B0E15", "#0B101A"),
        ("Tone_0C1115", "#0C1115", "#121B2B"),
        ("Tone_101215", "#101215", "#121B2B"),
        ("Tone_101317", "#101317", "#151D2B"),
        ("Tone_10151C", "#10151C", "#192338"),
        ("Tone_10161B", "#10161B", "#121B2B"),
        ("Tone_10171C", "#10171C", "#121B2B"),
        ("Tone_10191F", "#10191F", "#121B2B"),
        ("Tone_111822", "#111822", "#121B2B"),
        ("Tone_111B25", "#111B25", "#121B2B"),
        ("Tone_113139", "#113139", "#153B50"),
        ("Tone_113C41", "#113C41", "#16324A"),
        ("Tone_12343A", "#12343A", "#19394F"),
        ("Tone_153E46", "#153E46", "#19394F"),
        ("Tone_16313A", "#16313A", "#19394F"),
        ("Tone_173E48", "#173E48", "#23405B"),
        ("Tone_181D22", "#181D22", "#202E44"),
        ("Tone_1A303A", "#1A303A", "#29394E"),
        ("Tone_1B2228", "#1B2228", "#203149"),
        ("Tone_1B252D", "#1B252D", "#29394E"),
        ("Tone_1D4149", "#1D4149", "#23405B"),
        ("Tone_1F3345", "#1F3345", "#29394E"),
        ("Tone_202C38", "#202C38", "#29394E"),
        ("Tone_206C6F", "#206C6F", "#40536C"),
        ("Tone_215769", "#215769", "#40536C"),
        ("Tone_253940", "#253940", "#29394E"),
        ("Tone_256A79", "#256A79", "#40536C"),
        ("Tone_257D8C", "#257D8C", "#69DFFF"),
        ("Tone_263139", "#263139", "#29394E"),
        ("Tone_26333D", "#26333D", "#29394E"),
        ("Tone_263D48", "#263D48", "#29394E"),
        ("Tone_267E85", "#267E85", "#69DFFF"),
        ("Tone_273845", "#273845", "#29394E"),
        ("Tone_28424C", "#28424C", "#344B66"),
        ("Tone_286880", "#286880", "#40536C"),
        ("Tone_2B1A222D", "#2B1A222D", "#FF192338"),
        ("Tone_2B4950", "#2B4950", "#40536C"),
        ("Tone_2E9BA5", "#2E9BA5", "#548CB4"),
        ("Tone_327A8A", "#327A8A", "#69DFFF"),
        ("Tone_344651", "#344651", "#40536C"),
        ("Tone_34515D", "#34515D", "#40536C"),
        ("Tone_356D78", "#356D78", "#406E91"),
        ("Tone_365B6378", "#365B6378", "#FF40536C"),
        ("Tone_36639AA4", "#36639AA4", "#FF40536C"),
        ("Tone_3D5A64", "#3D5A64", "#40536C"),
        ("Tone_3DE7EA", "#3DE7EA", "#69DFFF"),
        ("Tone_43D9DC", "#43D9DC", "#69DFFF"),
        ("Tone_456A74", "#456A74", "#526B88"),
        ("Tone_45DFE7", "#45DFE7", "#69DFFF"),
        ("Tone_47616D", "#47616D", "#40536C"),
        ("Tone_48A6AF", "#48A6AF", "#69DFFF"),
        ("Tone_48D9E5", "#48D9E5", "#69DFFF"),
        ("Tone_4D458D98", "#4D458D98", "#655F82A8"),
        ("Tone_5A9EA9", "#5A9EA9", "#69DFFF"),
        ("Tone_5EE4D8", "#5EE4D8", "#69DFFF"),
        ("Tone_5FE7DD", "#5FE7DD", "#69DFFF"),
        ("Tone_6D91A5", "#6D91A5", "#A0AEC3"),
        ("Tone_71516B74", "#71516B74", "#FF40536C"),
        ("Tone_71808C", "#71808C", "#8190A6"),
        ("Tone_7184AAC0", "#7184AAC0", "#FF40536C"),
        ("Tone_7192A6", "#7192A6", "#A0AEC3"),
        ("Tone_74C6CA", "#74C6CA", "#69DFFF"),
        ("Tone_7E9FB4", "#7E9FB4", "#A0AEC3"),
        ("Tone_8AA7B9", "#8AA7B9", "#A0AEC3"),
        ("Tone_91B2C7", "#91B2C7", "#A0AEC3"),
        ("Tone_92A5B5", "#92A5B5", "#A0AEC3"),
        ("Tone_92B0C3", "#92B0C3", "#A0AEC3"),
        ("Tone_93BFD0", "#93BFD0", "#A0AEC3"),
        ("Tone_9CB5C8", "#9CB5C8", "#A0AEC3"),
        ("Tone_B2C9D7", "#B2C9D7", "#A0AEC3"),
        ("Tone_B592FF", "#B592FF", "#B3A0FF"),
        ("Tone_BBCBD8", "#BBCBD8", "#E2EAF7"),
        ("Tone_BDCDD8", "#BDCDD8", "#A0AEC3"),
        ("Tone_C3D9E8", "#C3D9E8", "#E2EAF7"),
        ("Tone_C5E9F3", "#C5E9F3", "#DDEAFB"),
        ("Tone_D3E8EE", "#D3E8EE", "#E2EAF7"),
        ("Tone_DBCCFA", "#DBCCFA", "#E1D9FF"),
        ("Tone_E4F2F6", "#E4F2F6", "#E2EAF7"),
        ("Tone_E5F8F6", "#E5F8F6", "#E2EAF7"),
        ("Tone_E9F7FD", "#E9F7FD", "#F0F5FF"),
        ("Tone_EAF4FA", "#EAF4FA", "#F0F5FF"),
        ("Tone_ED0A0C10", "#ED0A0C10", "#FF101827"),
        ("Tone_EF111418", "#EF111418", "#FF152033"),
        ("Tone_F007090C", "#F007090C", "#FF0B101A"),
        ("Tone_F015191E", "#F015191E", "#FF192338"),
        ("Tone_F0F8FD", "#F0F8FD", "#F0F5FF"),
        ("Tone_F3F9FF", "#F3F9FF", "#F0F5FF"),
        ("Tone_F506080B", "#F506080B", "#FF0B101A"),
        ("Tone_F5102026", "#F5102026", "#FF192338"),
        ("Tone_F5FCFF", "#F5FCFF", "#F0F5FF"),
        ("Tone_F708090C", "#F708090C", "#FF0B101A")
    ];
    private static Color Parse(string value)=>(Color)ColorConverter.ConvertFromString(value)!;
    private static SolidColorBrush BrushFor(string value)
    {
        var b=new SolidColorBrush(Parse(value));b.Freeze();return b;
    }
    private static LinearGradientBrush Gradient(string first,string middle,string last,double offset=.53)
    {
        var b=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,1) };
        b.GradientStops.Add(new GradientStop(Parse(first),0));
        b.GradientStops.Add(new GradientStop(Parse(middle),offset));
        b.GradientStops.Add(new GradientStop(Parse(last),1));
        b.Freeze();return b;
    }
    internal static void Initialize()
    {
        bool signal=false;
        try { using var key=Registry.CurrentUser.OpenSubKey(RegistryPath);signal=string.Equals(key?.GetValue("AppearanceTheme") as string,"signal",StringComparison.Ordinal); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        Apply(signal);
    }
    internal static void Apply(bool signal,bool purple=false,int glassMode=0,bool persist=false)
    {
        var next=new ResourceDictionary();
        foreach(var (key,classic,alternate) in Colors) next[key]=BrushFor(signal?alternate:classic);
        next["SignalOnly"]=signal?Visibility.Visible:Visibility.Collapsed;
        next["NavSpacing"]=new Thickness(0,0,0,signal?9:5);
        next["CardGradient"]=signal?Gradient("#121B2B","#101827","#192338"):Gradient("#E912151A","#EE090B0E","#ED0D1014");
        next["HeaderGradient"]=signal?Gradient("#0B101A","#121B2B","#192338",.65):Gradient("#F510141A","#F707090D","#F5101015",.65);
        next["IslandGradient"]=signal?Gradient("#121B2B","#0B101A","#192338",.58):Gradient("#FC0B0D10","#FE060709","#FC101014",.58);
        next["SignalHeroGradient"]=Gradient("#0B101A","#121B2B","#192338");
        foreach(int i in new[]{0,2,4,6})
            next["Kpi"+i+"Gradient"]=signal?(i%4==0?Gradient("#112B3D","#121B2B","#192338"):Gradient("#20213E","#121B2B","#252445")):next["CardGradient"];
        next["Accent"]=BrushFor(purple?(signal?"#B3A0FF":"#B392FF"):(signal?"#69DFFF":"#3DE7EA"));
        if(!signal) next["WindowBg"]=BrushFor(glassMode==1?"#F9050608":glassMode==2?"#FF050608":"#FD050608");
        var dictionaries=Application.Current.Resources.MergedDictionaries;
        if(_palette!=null&&dictionaries.Contains(_palette)) dictionaries[dictionaries.IndexOf(_palette)]=next;
        else dictionaries.Add(next);
        _palette=next;IsSignal=signal;
        if(persist)try { using var key=Registry.CurrentUser.CreateSubKey(RegistryPath);key?.SetValue("AppearanceTheme",signal?"signal":"classic",RegistryValueKind.String); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
