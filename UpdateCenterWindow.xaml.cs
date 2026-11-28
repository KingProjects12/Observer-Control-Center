using System.Windows;
using System.Diagnostics;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public partial class UpdateCenterWindow : Window
{
    private readonly MainWindow _main;
    private CancellationTokenSource? _installCancellation;
    private bool _installLaunching;

    public UpdateCenterWindow(MainWindow main)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        _main = main;
        RepoBox.Text = main.UpdatesRepository;
        bool official = GitHubReleaseClient.IsValidRepository(UpdateSettingsStore.EmbeddedRepository);
        RepoBox.IsReadOnly = official;
        RepoBox.ToolTip = official ? UiText.T("The official build has a fixed repository.", "Репозиторий официальной сборки зафиксирован при публикации.") : UiText.T("Enter owner/repo for a local prototype.", "Укажите owner/repo для локального прототипа.");
        AutoCheckBox.IsChecked = main.UpdatesAutoCheck;
        InstalledVersion.Text = UiText.T("Installed: ", "Установлено: ") + GitHubReleaseClient.AppVersionText;
        RefreshInfo();
    }

    internal void RefreshInfo()
    {
        if (_installCancellation != null || _installLaunching) return;
        StatusText.Text = _main.UpdateMessage;
        StatusText.Foreground=new Observer.ControlCenter.Models.ConsoleEntry(StatusText.Text,Observer.ControlCenter.Models.ConsoleEntry.Classify(StatusText.Text)).Color;
        var release = _main.LatestUpdate;
        InstallButton.Visibility = release?.Installer != null && string.Equals(_main.UpdatesRepository, UpdateInstaller.OfficialRepository, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = CheckButton.IsEnabled;
        DismissButton.Visibility = release != null ? Visibility.Visible : Visibility.Collapsed;
        ReleaseNotes.Text = release == null ? UiText.T("No new version found or check has not run yet.", "Новая версия не обнаружена или проверка ещё не выполнена.")
            : $"{release.Tag}  •  {(release.PublishedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? UiText.T("Unknown date", "Дата неизвестна"))}\n\n"
            + (string.IsNullOrWhiteSpace(release.Notes) ? UiText.T("The author did not provide release notes.", "Автор не добавил описание изменений.") : UiText.SelectNotes(release.Notes, UiText.IsRussian));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _main.SaveUpdateSettings(RepoBox.Text, AutoCheckBox.IsChecked == true);
            RefreshInfo();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, UiText.T("Update settings", "Настройки обновлений"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        // Persist the repo before checking. No login or API token is needed for public releases.
        try { _main.SaveUpdateSettings(RepoBox.Text, AutoCheckBox.IsChecked == true); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            StatusText.Text = ex.Message;
            return;
        }
        CheckButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        StatusText.Text = UiText.T("Checking GitHub Releases…", "Проверяю GitHub Releases…");
        try { await _main.CheckUpdatesAsync(manual: true); }
        finally { CheckButton.IsEnabled = true; RefreshInfo(); }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_installCancellation != null || _installLaunching || _main.LatestUpdate is not { Installer: { } asset } release) return;
        if (!SelfUpdate.HasOriginalName(Environment.ProcessPath))
        {
            StatusText.Text = UiText.T("Rename the EXE to Observer.ControlCenter.exe before updating.","Перед обновлением верните файлу имя Observer.ControlCenter.exe.");
            return;
        }
        if (_main.HasActiveOperation)
        {
            StatusText.Text = UiText.T("Wait for the active operation to finish, then retry.","Дождитесь завершения операции и повторите обновление.");
            return;
        }
        if (MessageBox.Show(this,
            (UiText.IsRussian ? $"Обновить Observer до {release.Tag} ({asset.Size / 1024.0 / 1024.0:F1} МиБ)?\n\n" : $"Update Observer to {release.Tag} ({asset.Size / 1024.0 / 1024.0:F1} MiB)?\n\n") +
            UiText.T("Only the EXE will be downloaded. Observer will close, replace the file and restart. Save important reports. The EXE may be unsigned; do not disable Windows security.", "Будет скачан только EXE, программа закроется, заменит файл и запустится снова. Сохраните важные отчёты. EXE может быть без цифровой подписи; не отключайте защиту Windows."),
            UiText.T("Observer update", "Обновление Observer"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        using var cancellation = new CancellationTokenSource();
        _installCancellation = cancellation;
        CheckButton.IsEnabled = InstallButton.IsEnabled = SaveButton.IsEnabled = OpenButton.IsEnabled = DismissButton.IsEnabled = false;
        AutoCheckBox.IsEnabled = RepoBox.IsEnabled = false;
        CancelInstallButton.Visibility = InstallProgress.Visibility = Visibility.Visible;
        CancelInstallButton.IsEnabled = true;
        InstallProgress.Value = 0;
        StatusText.Text = UiText.T("Fetching checksum and new EXE…", "Получаю контрольную сумму и новый EXE…");
        VerifiedUpdate? downloaded = null;
        try
        {
            var progress = new Progress<int>(percent =>
            {
                if (_installCancellation != cancellation || _installLaunching) return;
                InstallProgress.Value = percent;
                StatusText.Text = percent == 100 ? UiText.T("Verifying SHA-256…", "Проверяю SHA-256…") : (UiText.IsRussian ? $"Загрузка обновления: {percent}%" : $"Downloading update: {percent}%");
            });
            downloaded = await UpdateInstaller.DownloadAsync(asset, progress, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            StatusText.Text = UiText.T("SHA-256 verified. Restarting Observer…", "SHA-256 совпадает. Перезапускаю Observer…");
            StatusText.Foreground=System.Windows.Media.Brushes.LightGreen;
            CancelInstallButton.IsEnabled = false;
            if (_main.HasActiveOperation)
                throw new InvalidOperationException(UiText.T("An operation is running. Retry after it finishes.","Идёт операция. Повторите обновление после её завершения."));
            SelfUpdate.StartHelper(downloaded);
            _installLaunching = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            if (!_installLaunching && downloaded != null) UpdateInstaller.TryDelete(Path.GetDirectoryName(downloaded.Path)!);
            StatusText.Foreground=ex is OperationCanceledException?System.Windows.Media.Brushes.Goldenrod:System.Windows.Media.Brushes.Salmon;
            StatusText.Text = ex is OperationCanceledException
                ? UiText.T("Download cancelled or timed out.", "Загрузка отменена или превышено время ожидания.")
                : UiText.T("Update failed: ", "Обновление не выполнено: ") + ex.Message;
        }
        finally
        {
            _installCancellation = null;
            if (!_installLaunching)
            {
                CheckButton.IsEnabled = InstallButton.IsEnabled = SaveButton.IsEnabled = OpenButton.IsEnabled = DismissButton.IsEnabled = true;
                AutoCheckBox.IsEnabled = RepoBox.IsEnabled = true;
                CancelInstallButton.Visibility = InstallProgress.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void CancelInstall_Click(object sender, RoutedEventArgs e) => _installCancellation?.Cancel();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_installCancellation != null && !_installLaunching)
        {
            _installCancellation.Cancel();
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _main.SaveUpdateSettings(RepoBox.Text, AutoCheckBox.IsChecked == true);
            _main.OpenReleasePage();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { StatusText.Text = ex.Message; }
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        try { _main.DismissAvailableUpdate(); StatusText.Text = UiText.T("This version is dismissed until a new version appears.", "Уведомление об этой версии скрыто до появления новой."); DismissButton.Visibility = Visibility.Collapsed; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; }
    }
}
