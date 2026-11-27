using System.Windows;
using System.Windows.Controls;
using Observer.ControlCenter.Services;
namespace Observer.ControlCenter;
public partial class MainWindow
{
    private void AccessAnalyzer_Click(object sender,RoutedEventArgs e)=>ShowModule("connections");
    private void LegacyCollect_Click(object sender,RoutedEventArgs e)=>ShowModule("routers");
    private void LegacyVpn_Click(object sender,RoutedEventArgs e){ShowModule("routers");_collector?.SetMode("nat",true);}
    private void LegacyTraffic_Click(object sender,RoutedEventArgs e)=>ShowModule("traffic");
    private void LegacyTools_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu { PlacementTarget=(UIElement)sender, Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom };
        void Item(string en,string ru,RoutedEventHandler handler){var item=new MenuItem{Header=UiText.T(en,ru)};item.Click+=handler;menu.Items.Add(item);}
        void Group(string en,string ru) { if(menu.Items.Count>0)menu.Items.Add(new Separator()); menu.Items.Add(new MenuItem { Header=UiText.T(en,ru), IsEnabled=false }); }
        Group("REPORTS","ОТЧЁТЫ");
        Item("Import CSV","Импорт CSV",Import_Click);
        Item("Open reports","Открыть отчёты",Reports_Click);
        Item("Local connections table","Локальная таблица соединений",Connections_Click);
        Item("Local traffic table","Локальная таблица трафика",Traffic_Click);
        Group("DIAGNOSTICS","ДИАГНОСТИКА");
        Item("Check TCP 80/443","Проверить TCP 80/443",RouterCheck_Click);
        Group("APPEARANCE","ОФОРМЛЕНИЕ");
        Item("Change accent","Сменить акцент",Theme_Click);
        var glass = new MenuItem { Header = GlassModeButton.Content?.ToString() ?? UiText.X022 }; glass.Click += Glass_Click; menu.Items.Add(glass);
        Group("SYSTEM","СИСТЕМА");
        Item("Updates","Обновления",Updates_Click);
        Item("Guide / F1","Справка / F1",Help_Click);
        var selected=BeginNavigationAction(NavTools);
        menu.Closed+=(_,_)=>selected.Dispose();
        try { menu.IsOpen=true; }
        catch { selected.Dispose(); throw; }
    }
}
