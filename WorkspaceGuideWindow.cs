using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public partial class MainWindow
{
    private void Help_Click(object sender, RoutedEventArgs e) => new WorkspaceGuideWindow { Owner = this }.ShowDialog();
}

internal sealed class WorkspaceGuideWindow : Window
{
    private static string T(string en,string ru)=>UiText.T(en,ru);
    public WorkspaceGuideWindow()
    {
        Title=T("Observer · Workspace guide","Observer · Справка");
        Width=810; Height=730; MinWidth=550; MinHeight=450; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(7,9,12)); Foreground=Brushes.White; FontFamily=new FontFamily("Segoe UI");
        DarkTitleBar.Apply(this);
        var root=new StackPanel{Margin=new Thickness(24)};
        Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock{Text=T("WORKSPACE / QUICK GUIDE","РАБОЧАЯ ОБЛАСТЬ / СПРАВКА"),FontSize=22,FontWeight=FontWeights.Bold,Foreground=(Brush)FindResource("Accent"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
        void Group(string en,string ru,params (string En,string Ru,string DescEn,string DescRu)[] rows)
        {
            var stack=new StackPanel();root.Children.Add(new Border{Style=(Style)FindResource("CardBorder"),Child=stack,Margin=new Thickness(0,0,0,12)});
            stack.Children.Add(new TextBlock{Text=T(en,ru),FontWeight=FontWeights.Bold,Foreground=(Brush)FindResource("Accent"),Margin=new Thickness(0,0,0,8)});
            foreach(var r in rows)
            {
                stack.Children.Add(new TextBlock{Text=T(r.En,r.Ru),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,7,0,3),TextWrapping=TextWrapping.Wrap});
                stack.Children.Add(new TextBlock{Text=T(r.DescEn,r.DescRu),Foreground=(Brush)FindResource("TextSecondary"),TextWrapping=TextWrapping.Wrap});
            }
        }
        Group("01 / ANALYTICS","01 / АНАЛИТИКА",
            ("Overview","Обзор","Summary of the loaded traffic report.","Сводка загруженного отчёта трафика."),
            ("Connections","Соединения","Inspect source, destination, ports and protocol in a CSV report.","Просмотр источника, назначения, портов и протокола из CSV."),
            ("TOP Traffic","ТОП трафика","View the ranking by traffic volume, not current speed.","Рейтинг по объёму, а не текущей скорости."),
            ("Filter","Поиск","Filter the displayed table. Advanced search is planned next.","Фильтр отображаемой таблицы. Расширенный поиск — следующий этап."));
        Group("02 / COLLECTION","02 / СБОР ДАННЫХ",
            ("Connect / NAT","Подключение / NAT","Enter the router, credentials and office CIDR; then Collect NAT.","Введите роутер, логин, пароль и сеть офиса; затем «Собрать NAT»."),
            ("VPN REMOTE","VPN REMOTE","Use a pre-existing authorized VPN; does not establish a tunnel.","Использовать уже подключённый разрешённый VPN; туннель не создаётся."),
            ("Collect traffic","Собрать трафик","Choose one monitor period and collect TOP Traffic.","Выберите один период монитора и соберите TOP Traffic."),
            ("HTTPS","HTTPS","Prefer trusted HTTPS. HTTP is not encrypted.","Предпочтителен доверенный HTTPS. HTTP не шифруется."),
            ("Snapshots / interval","Снимки / интервал","Number of NAT observations and time between them.","Количество наблюдений NAT и пауза между ними."),
            ("VPN / TCP","VPN / TCP","Check TCP reachability; not proof of a VPN route or successful login.","Проверка доступности TCP; не подтверждает VPN-маршрут или вход."),
            ("Stop","Стоп","Cancel active collection.","Остановить текущий сбор."),
            ("Tools: central import / demo","Инструменты: центральный импорт / демо","Import an authorized gateway CSV or run an offline sample.","Импорт CSV разрешённого шлюза или офлайн-пример."));
        Group("03 / REPORTS","03 / ОТЧЁТЫ",
            ("Report selector","Выбор отчёта","Select office destinations, related flows, all connections, clients or traffic reports.","Выберите назначения в офис, связанные потоки, все соединения, клиентов или отчёты трафика."),
            ("Show in Observer","Показать в Observer","Load the selected CSV into the main window.","Загрузить выбранный CSV в главное окно."),
            ("Open Excel","Открыть Excel","Open the generated workbook with the installed spreadsheet application.","Открыть сформированную книгу в установленном табличном приложении."),
            ("Reports / Open reports","Отчёты / Открыть отчёты","Open the local report folder.","Открыть локальную папку отчётов."),
            ("Load CSV","Загрузить CSV","Import an existing local report.","Импорт готового локального отчёта."),
            ("Copy log","Копировать журнал","Copy diagnostic text. Review sensitive details before sharing.","Скопировать диагностику. Перед отправкой проверьте конфиденциальные данные."));
        Group("04 / SYSTEM","04 / СИСТЕМА",
            ("Tools","Инструменты","Secondary controls grouped into reports, diagnostics, appearance and system.","Дополнительные действия: отчёты, диагностика, оформление, система."),
            ("Check TCP","Проверить TCP","Check ports 80 and 443, without router login.","Проверка портов 80 и 443 без входа на роутер."),
            ("Change accent / Glass","Сменить акцент / Стекло","Change accent and density of the existing glass theme.","Сменить акцент и плотность существующего стекла."),
            ("Updates / Save / Check now","Обновления / Сохранить / Проверить сейчас","Save update preferences or check for an official release.","Сохранить настройку автопроверки или проверить официальный релиз."),
            ("Update and restart / Cancel","Обновить и перезапустить / Отмена","Download and verify EXE; cancel while downloading.","Скачать и проверить EXE; отменить во время загрузки."),
            ("Open GitHub / Skip version","Открыть GitHub / Пропустить версию","View release details or dismiss that version's notification.","Открыть описание релиза или скрыть уведомление о версии."),
            ("Island / Window controls","Островок / Управление окном","Compact mode, minimize, maximize/restore and close.","Компактный режим, свернуть, развернуть/восстановить и закрыть."));
        var close=new Button{Content=T("CLOSE","ЗАКРЫТЬ"),Style=(Style)FindResource("AccentButton"),HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};
        close.Click+=(_,_)=>Close();root.Children.Add(close);
    }
}
