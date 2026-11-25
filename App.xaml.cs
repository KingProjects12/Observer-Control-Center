using System.Windows;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        UiText.Initialize();
        AppearanceTheme.Initialize();
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--ui-smoke")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { UiSmoke.Run(); File.WriteAllText(e.Args[1], "PASS: EN and RU UI construction, navigation and control states"); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(e.Args[1], ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == SelfUpdate.HelperArgument)
        {
            try { SelfUpdate.RunHelper(e.Args); }
            catch (Exception ex) { MessageBox.Show(ex.Message, UiText.T("Observer · update error", "Observer · ошибка обновления"), MessageBoxButton.OK, MessageBoxImage.Error); }
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(UiText.T("Observer UI error: ", "Ошибка интерфейса Observer: ") + args.Exception.Message,
                "Observer Control Center", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
