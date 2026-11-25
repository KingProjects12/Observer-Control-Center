using System.Windows;
using System.Windows.Controls;
using Observer.ControlCenter.Services;
namespace Observer.ControlCenter;
public partial class MainWindow
{
    private void LegacyCollect_Click(object sender,RoutedEventArgs e)=>OpenLegacyCollector();
    private void LegacyVpn_Click(object sender,RoutedEventArgs e)=>OpenLegacyCollector(vpn:true);
    private void LegacyTraffic_Click(object sender,RoutedEventArgs e)=>OpenLegacyCollector(traffic:true);
    private void OpenLegacyCollector(bool vpn=false,bool traffic=false)
    {
        var window=new LegacyCollectorWindow(RouterIP.Text.Trim(),vpn,traffic){Owner=this};
        window.ShowDialog();RouterIP.Text=window.Address;
        if(window.SelectedCsv is not { } path)return;
        if(Path.GetFileName(path).StartsWith("08_Top_Traffic",StringComparison.OrdinalIgnoreCase)){LoadTraffic(path);ShowPage("traffic");}
        else {LoadConnections(path);ShowPage("connections");}
        _reportPath=path;PageSubtitle.Text=Path.GetFileName(path);AddLog("REPORT // "+Path.GetFileName(path));
    }
    private void LegacyTools_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu { PlacementTarget=(UIElement)sender, Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom };
        void Item(string en,string ru,RoutedEventHandler handler){var item=new MenuItem{Header=UiText.T(en,ru)};item.Click+=handler;menu.Items.Add(item);}
        Item("Import CSV","Импорт CSV",Import_Click);
        Item("Open reports","Открыть отчёты",Reports_Click);
        Item("Check TCP 80/443","Проверить TCP 80/443",RouterCheck_Click);
        Item("Change accent","Сменить акцент",Theme_Click);
        Item("Glass density","Прозрачность стекла",Glass_Click);
        Item("Updates","Обновления",Updates_Click);
        menu.IsOpen=true;
    }
}
