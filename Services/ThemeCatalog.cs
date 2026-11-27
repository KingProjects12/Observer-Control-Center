namespace Observer.ControlCenter.Services;

internal sealed record ThemePreset(string Id,string En,string Ru,string Background,string Panel,string Field,string Stroke,string Accent,string Secondary,string End,string Art)
{
    internal string Name=>UiText.T(En,Ru);
    internal bool HasSnow=>Id is "winter" or "newyear";
}
internal static class ThemeCatalog
{
    internal static IReadOnlyList<ThemePreset> All {get;}=new ThemePreset[]
    {
        new("classic","Black Liquid Glass","Жидкое стекло","#0D0E10","#192121","#263030","#394747","#3DE7EA","#B392FF","#191A1E","glass"),
        new("signal","Signal","Signal","#10141B","#1A2431","#253345","#303945","#69DFFF","#B3A0FF","#192338","signal"),
        new("spring","Spring","Весна","#14221D","#203229","#304439","#486552","#9EE5B4","#F5B5D3","#33263A","spring"),
        new("summer","Summer","Лето","#10252F","#193542","#244554","#45616A","#78DDF1","#F6D580","#3A3427","summer"),
        new("autumn","Autumn","Осень","#241A15","#38281F","#47352B","#6A4C38","#FFC08A","#ED9C70","#422A20","autumn"),
        new("winter","Winter","Зима","#111D2C","#202F42","#2B3C53","#48617C","#B2DDFF","#D4C8FF","#293548","winter"),
        new("newyear","New Year","Новый год","#10251F","#1C352C","#2B473B","#4B6754","#F8D58C","#9DE8CF","#39212E","newyear"),
        new("midnight","Midnight","Полночь","#08090B","#111417","#1B2024","#333B40","#79E6B4","#95B9AD","#08090B","plain"),
        new("graphite","Graphite","Графит","#17191C","#23262B","#30343A","#444951","#86C6FF","#B2C0D5","#17191C","plain"),
        new("ocean","Ocean","Океан","#102334","#183449","#23445D","#365D75","#6FD3FF","#A2E7E1","#102334","plain"),
        new("forest","Forest","Лес","#11251E","#1B352A","#294638","#40644F","#86E2AE","#C5D88F","#11251E","plain"),
        new("sunset","Sunset","Закат","#27180F","#39261A","#4B3223","#735139","#FFC079","#F0998C","#27180F","plain"),
        new("rose","Rose","Роза","#291522","#3B2433","#4E3142","#70475B","#FF9EBD","#E5B0DE","#291522","plain"),
        new("lavender","Lavender","Лаванда","#1F1930","#302642","#403451","#61517C","#C8B5FF","#E6B8EB","#1F1930","plain"),
        new("aurora","Aurora","Аврора","#10242C","#1B3440","#294654","#466678","#8DEBD8","#B7A4FF","#322449","wave"),
        new("nebula","Nebula","Туманность","#1D1930","#2A2743","#3B3855","#615D7C","#C8ACFF","#89DFFF","#3A203D","stars"),
        new("ember","Ember","Уголь и пламя","#211619","#342327","#463136","#64434A","#FFAC91","#F6D08E","#36281B","wave"),
        new("arctic","Arctic","Арктика","#14232B","#21343E","#304752","#4D6977","#B8E8FF","#83DCD4","#182F3B","wave")
    };
    internal static ThemePreset Get(string? id)=>All.FirstOrDefault(x=>x.Id==id)??All[0];
}
