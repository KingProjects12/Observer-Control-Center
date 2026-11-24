using System.Windows;

namespace Observer.ControlCenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Ошибка интерфейса Observer: " + args.Exception.Message,
                "Observer Control Center", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
