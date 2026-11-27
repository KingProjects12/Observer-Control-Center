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
        ("WindowBg", "#FD0D0E10", "#FF10141B"),
        ("PanelBg", "#F016171A", "#FF191E27"),
        ("PanelBg2", "#F0202125", "#FF222934"),
        ("Stroke", "#34363B", "#303945"),
        ("Accent", "#3DE7EA", "#69DFFF"),
        ("Accent2", "#B392FF", "#B3A0FF"),
        ("TextMain", "#F1F2F4", "#EFF2F7"),
        ("TextSecondary", "#A4A8B0", "#A8B2C2"),
        ("Positive", "#3BD5AA", "#3BD5AA"),
        ("DialogBg", "#0D0E10", "#10141B"),
        ("DialogText", "#F1F2F4", "#EFF2F7"),
        ("DialogAccent", "#40E0D0", "#69DFFF"),
        ("DialogField", "#202125", "#222934"),
        ("DialogFieldStroke", "#34363B", "#303945"),
        ("DialogLog", "#0D0E10", "#10141B"),
        ("Tone_06090F", "#0D0E10", "#10141B"),
        ("Tone_070809", "#0D0E10", "#10141B"),
        ("Tone_07090B", "#0D0E10", "#10141B"),
        ("Tone_080C12", "#0D0E10", "#10141B"),
        ("Tone_090B0E", "#0D0E10", "#10141B"),
        ("Tone_090C10", "#0D0E10", "#10141B"),
        ("Tone_0A1015", "#16171A", "#191E27"),
        ("Tone_0B0D10", "#0D0E10", "#10141B"),
        ("Tone_0B0E15", "#0D0E10", "#10141B"),
        ("Tone_0C1115", "#16171A", "#191E27"),
        ("Tone_101215", "#16171A", "#191E27"),
        ("Tone_101317", "#16171A", "#191E27"),
        ("Tone_10151C", "#16171A", "#191E27"),
        ("Tone_10161B", "#16171A", "#191E27"),
        ("Tone_10171C", "#16171A", "#191E27"),
        ("Tone_10191F", "#16171A", "#191E27"),
        ("Tone_111822", "#16171A", "#191E27"),
        ("Tone_111B25", "#16171A", "#191E27"),
        ("Tone_113139", "#202125", "#222934"),
        ("Tone_113C41", "#202125", "#222934"),
        ("Tone_12343A", "#202125", "#222934"),
        ("Tone_153E46", "#202125", "#222934"),
        ("Tone_16313A", "#202125", "#222934"),
        ("Tone_173E48", "#202125", "#222934"),
        ("Tone_181D22", "#16171A", "#191E27"),
        ("Tone_1A303A", "#202125", "#222934"),
        ("Tone_1B2228", "#16171A", "#191E27"),
        ("Tone_1B252D", "#202125", "#222934"),
        ("Tone_1D4149", "#202125", "#222934"),
        ("Tone_1F3345", "#202125", "#222934"),
        ("Tone_202C38", "#202125", "#222934"),
        ("Tone_206C6F", "#34363B", "#303945"),
        ("Tone_215769", "#34363B", "#303945"),
        ("Tone_253940", "#202125", "#222934"),
        ("Tone_256A79", "#34363B", "#303945"),
        ("Tone_257D8C", "#34363B", "#303945"),
        ("Tone_263139", "#202125", "#222934"),
        ("Tone_26333D", "#202125", "#222934"),
        ("Tone_263D48", "#202125", "#222934"),
        ("Tone_267E85", "#34363B", "#303945"),
        ("Tone_273845", "#202125", "#222934"),
        ("Tone_28424C", "#34363B", "#303945"),
        ("Tone_286880", "#34363B", "#303945"),
        ("Tone_2B1A222D", "#202125", "#222934"),
        ("Tone_2B4950", "#34363B", "#303945"),
        ("Tone_2E9BA5", "#A4A8B0", "#A8B2C2"),
        ("Tone_327A8A", "#34363B", "#303945"),
        ("Tone_344651", "#34363B", "#303945"),
        ("Tone_34515D", "#34363B", "#303945"),
        ("Tone_356D78", "#34363B", "#303945"),
        ("Tone_365B6378", "#34363B", "#303945"),
        ("Tone_36639AA4", "#A4A8B0", "#A8B2C2"),
        ("Tone_3D5A64", "#34363B", "#303945"),
        ("Tone_3DE7EA", "#3DE7EA", "#69DFFF"),
        ("Tone_43D9DC", "#43D9DC", "#69DFFF"),
        ("Tone_456A74", "#34363B", "#303945"),
        ("Tone_45DFE7", "#45DFE7", "#69DFFF"),
        ("Tone_47616D", "#34363B", "#303945"),
        ("Tone_48A6AF", "#48A6AF", "#69DFFF"),
        ("Tone_48D9E5", "#48D9E5", "#69DFFF"),
        ("Tone_4D458D98", "#4D458D98", "#655F82A8"),
        ("Tone_5A9EA9", "#5A9EA9", "#69DFFF"),
        ("Tone_5EE4D8", "#5EE4D8", "#69DFFF"),
        ("Tone_5FE7DD", "#5FE7DD", "#69DFFF"),
        ("Tone_6D91A5", "#A4A8B0", "#A8B2C2"),
        ("Tone_71516B74", "#34363B", "#303945"),
        ("Tone_71808C", "#A4A8B0", "#A8B2C2"),
        ("Tone_7184AAC0", "#A4A8B0", "#A8B2C2"),
        ("Tone_7192A6", "#A4A8B0", "#A8B2C2"),
        ("Tone_74C6CA", "#74C6CA", "#69DFFF"),
        ("Tone_7E9FB4", "#A4A8B0", "#A8B2C2"),
        ("Tone_8AA7B9", "#A4A8B0", "#A8B2C2"),
        ("Tone_91B2C7", "#A4A8B0", "#A8B2C2"),
        ("Tone_92A5B5", "#A4A8B0", "#A8B2C2"),
        ("Tone_92B0C3", "#A4A8B0", "#A8B2C2"),
        ("Tone_93BFD0", "#A4A8B0", "#A8B2C2"),
        ("Tone_9CB5C8", "#A4A8B0", "#A8B2C2"),
        ("Tone_B2C9D7", "#F1F2F4", "#EFF2F7"),
        ("Tone_B592FF", "#B592FF", "#B3A0FF"),
        ("Tone_BBCBD8", "#F1F2F4", "#EFF2F7"),
        ("Tone_BDCDD8", "#F1F2F4", "#EFF2F7"),
        ("Tone_C3D9E8", "#F1F2F4", "#EFF2F7"),
        ("Tone_C5E9F3", "#F1F2F4", "#EFF2F7"),
        ("Tone_D3E8EE", "#F1F2F4", "#EFF2F7"),
        ("Tone_DBCCFA", "#F1F2F4", "#EFF2F7"),
        ("Tone_E4F2F6", "#F1F2F4", "#EFF2F7"),
        ("Tone_E5F8F6", "#F1F2F4", "#EFF2F7"),
        ("Tone_E9F7FD", "#F1F2F4", "#EFF2F7"),
        ("Tone_EAF4FA", "#F1F2F4", "#EFF2F7"),
        ("Tone_ED0A0C10", "#0D0E10", "#10141B"),
        ("Tone_EF111418", "#16171A", "#191E27"),
        ("Tone_F007090C", "#0D0E10", "#10141B"),
        ("Tone_F015191E", "#16171A", "#191E27"),
        ("Tone_F0F8FD", "#F1F2F4", "#EFF2F7"),
        ("Tone_F3F9FF", "#F1F2F4", "#EFF2F7"),
        ("Tone_F506080B", "#0D0E10", "#10141B"),
        ("Tone_F5102026", "#16171A", "#191E27"),
        ("Tone_F5FCFF", "#F1F2F4", "#EFF2F7"),
        ("Tone_F708090C", "#0D0E10", "#10141B")
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
        next["NavSpacing"]=new Thickness(0,0,0,8);
        foreach(var (id,dark,navy,accent) in new[]{
            ("routers","#152527","#192B30","#69DFFF"),("connections","#1B222D","#1D2939","#88ADFF"),
            ("tcp","#24202D","#28253A","#B3A0FF"),("traffic","#28212A","#2E2635","#E4A0CD"),
            ("issues","#28251E","#302C24","#FFD27E")})
        {next["Module_"+id+"_Bg"]=BrushFor(signal?navy:dark);next["Module_"+id+"_Fg"]=BrushFor(accent);}
        next["CardGradient"]=signal?BrushFor("#191E27"):Gradient("#F21B1C20","#F016171A","#F0191A1E");
        next["HeaderGradient"]=signal?BrushFor("#10141B"):Gradient("#F21B1C20","#F016171A","#F0191A1E");
        next["IslandGradient"]=signal?BrushFor("#191E27"):Gradient("#FC1B1C20","#FE101113","#FC191A1E");
        next["SignalHeroGradient"]=next["HeaderGradient"];
        next["BrandLine"]=BrushFor(signal?"#40536C":"#34515D");
        next["BrandNode"]=BrushFor(signal?"#121B2B":"#080C12");
        foreach(int i in new[]{0,2,4,6})
            next["Kpi"+i+"Gradient"]=next["CardGradient"];
        next["Accent"]=BrushFor(purple?(signal?"#B3A0FF":"#B392FF"):(signal?"#69DFFF":"#3DE7EA"));
        if(!signal) next["WindowBg"]=BrushFor(glassMode==1?"#F90D0E10":glassMode==2?"#FF0D0E10":"#FD0D0E10");
        var dictionaries=Application.Current.Resources.MergedDictionaries;
        if(_palette!=null&&dictionaries.Contains(_palette)) dictionaries[dictionaries.IndexOf(_palette)]=next;
        else dictionaries.Add(next);
        _palette=next;IsSignal=signal;
        if(persist)try { using var key=Registry.CurrentUser.CreateSubKey(RegistryPath);key?.SetValue("AppearanceTheme",signal?"signal":"classic",RegistryValueKind.String); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
