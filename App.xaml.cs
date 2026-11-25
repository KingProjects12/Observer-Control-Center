using System.Windows;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] == SelfUpdate.HelperArgument)
        {
            try { SelfUpdate.RunHelper(e.Args); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Observer · ошибка обновления", MessageBoxButton.OK, MessageBoxImage.Error); }
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Ошибка интерфейса Observer: " + args.Exception.Message,
                "Observer Control Center", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
