namespace Observer.ControlCenter.Services;

internal sealed record ThemePreset(string Id,string En,string Ru,string Background,string Panel,string Field,string Stroke,string Accent,string Secondary,string End,string Art,string Collection="original")
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
        new("blueprint","Blueprint","Чертёж","#101D30","#192C43","#253950","#3A5772","#A2D5FF","#C0DFEF","#1B3555","grid","studio"),
        new("carbon","Carbon","Карбон","#101215","#1C2025","#292F37","#414A54","#D0E1EB","#94C5D5","#222A31","carbon","studio"),
        new("copper","Copper Circuit","Медная плата","#201914","#30251E","#40342C","#634C3A","#F6BE8E","#9ED8CB","#2C3530","circuit","studio"),
        new("terminal","Terminal 84","Терминал 84","#0C1912","#14271B","#20372A","#3A5945","#9BEC9C","#D2E8A0","#15291B","scan","studio"),
        new("slate","Slate & Gold","Сланец и золото","#16191F","#242A32","#303841","#4B515B","#F3D48E","#B8C9E0","#332B23","facets","studio"),
        new("porcelain","Night Porcelain","Ночной фарфор","#18232D","#24333E","#32434F","#526676","#E2EAF2","#9DDDEB","#213A45","rings","studio"),
        new("sakura","Sakura Dusk","Сакура","#241A27","#342536","#453449","#674B69","#FFD0E2","#C6BEFF","#342B4C","petals","worlds"),
        new("tokyo","Tokyo After Rain","Токио после дождя","#171A2B","#262840","#34374F","#565778","#FFB1D7","#91DFFF","#37213D","city","worlds"),
        new("dunes","Desert Dunes","Дюны","#261E17","#382C22","#49392D","#69513D","#FFE0A4","#FFB89E","#473024","dunes","worlds"),
        new("lagoon","Bioluminescent Lagoon","Светящаяся лагуна","#10252B","#19353B","#26444A","#406168","#9CF7D8","#9CCBFF","#163D45","bubbles","worlds"),
        new("volcano","Volcanic Glass","Вулканическое стекло","#23191D","#33252A","#423239","#63494C","#FFB4A0","#FFE0A0","#412420","fault","worlds"),
        new("moon","Moonlit Mountains","Лунные горы","#151D2D","#242F43","#324058","#52637C","#D1E3FF","#B7C5EF","#253047","mountain","worlds"),
        new("eclipse","Solar Eclipse","Затмение","#17151D","#26232E","#363240","#554E60","#FFD7A6","#C4B8FD","#352339","eclipse","worlds"),
        new("saturn","Saturn Station","Станция Сатурн","#201C2D","#302A40","#3F3851","#5E5375","#F1D4AD","#BECCFF","#352644","orbit","worlds"),
        new("deepsea","Abyssal Station","Глубоководная станция","#101D29","#192E3B","#263D4A","#415D6F","#9DE8E8","#A9BDFF","#14333C","bubbles","worlds"),
        new("rain","Rain on Glass","Дождь на стекле","#14202A","#22323E","#303F4E","#495F73","#B2D9FF","#C4D9CE","#253648","rain","worlds"),
        new("prism","Prism","Призма","#1E1C30","#2D2941","#3D3852","#5A527B","#D7C0FF","#A5F5DA","#213D48","facets","lab"),
        new("synth","Synthwave","Синтвейв","#211731","#342442","#453253","#674B78","#FFA8DE","#A0E6FF","#43254F","grid","lab"),
        new("matrix","Digital Garden","Цифровой сад","#10201A","#1B3026","#284237","#41614E","#B6F5A5","#81E9D7","#244537","circuit","lab"),
        new("oxide","Oxide","Оксид","#25201C","#352D29","#463B36","#655249","#FFC6A8","#A6DDDD","#384049","fault","lab"),
        new("royal","Royal Ink","Королевские чернила","#1C1931","#2C2644","#3D3655","#5F537B","#D4BBFF","#F4D19B","#302644","rings","lab"),
        new("candy","Neon Candy","Неоновая конфета","#281B2D","#38283E","#49354E","#6C4C70","#FFB6D9","#9EEAFF","#302B50","bubbles","lab"),
        new("icefire","Ice & Fire","Лёд и пламя","#18222F","#28323D","#37424D","#56616E","#C4E7FF","#FFBE99","#46292A","facets","lab"),
        new("noir","Noir Cinema","Нуар","#111214","#1E2024","#2C2E32","#484B50","#E6E0D3","#C3B9A6","#292725","scan","lab")
    };
    internal static ThemePreset Get(string? id)=>All.FirstOrDefault(x=>x.Id==id)??All[0];
}
