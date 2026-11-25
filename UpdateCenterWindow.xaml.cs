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
        RepoBox.ToolTip = official ? "Репозиторий официальной сборки зафиксирован при публикации." : "Укажите owner/repo для локального прототипа.";
        AutoCheckBox.IsChecked = main.UpdatesAutoCheck;
        InstalledVersion.Text = "Установлено: " + GitHubReleaseClient.AppVersionText;
        RefreshInfo();
    }

    internal void RefreshInfo()
    {
        if (_installCancellation != null || _installLaunching) return;
        StatusText.Text = _main.UpdateMessage;
        var release = _main.LatestUpdate;
        InstallButton.Visibility = release?.Installer != null && string.Equals(_main.UpdatesRepository, UpdateInstaller.OfficialRepository, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = CheckButton.IsEnabled;
        DismissButton.Visibility = release != null ? Visibility.Visible : Visibility.Collapsed;
        ReleaseNotes.Text = release == null ? "Новая версия не обнаружена или проверка ещё не выполнена."
            : $"{release.Tag}  •  {(release.PublishedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Дата неизвестна")}\n\n"
            + (string.IsNullOrWhiteSpace(release.Notes) ? "Автор не добавил описание изменений." : release.Notes);
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
            MessageBox.Show(this, ex.Message, "Настройки обновлений", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        StatusText.Text = "Проверяю GitHub Releases…";
        try { await _main.CheckUpdatesAsync(manual: true); }
        finally { CheckButton.IsEnabled = true; RefreshInfo(); }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_installCancellation != null || _installLaunching || _main.LatestUpdate is not { Installer: { } asset } release) return;
        if (MessageBox.Show(this,
            $"Обновить Observer до {release.Tag} ({asset.Size / 1024.0 / 1024.0:F1} МиБ)?\n\n" +
            "Будет скачан только EXE, программа закроется, заменит файл и запустится снова. Сохраните важные отчёты. EXE может быть без цифровой подписи; не отключайте защиту Windows.",
            "Обновление Observer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        using var cancellation = new CancellationTokenSource();
        _installCancellation = cancellation;
        CheckButton.IsEnabled = InstallButton.IsEnabled = SaveButton.IsEnabled = OpenButton.IsEnabled = DismissButton.IsEnabled = false;
        AutoCheckBox.IsEnabled = RepoBox.IsEnabled = false;
        CancelInstallButton.Visibility = InstallProgress.Visibility = Visibility.Visible;
        CancelInstallButton.IsEnabled = true;
        InstallProgress.Value = 0;
        StatusText.Text = "Получаю контрольную сумму и новый EXE…";
        VerifiedUpdate? downloaded = null;
        try
        {
            var progress = new Progress<int>(percent =>
            {
                if (_installCancellation != cancellation || _installLaunching) return;
                InstallProgress.Value = percent;
                StatusText.Text = percent == 100 ? "Проверяю SHA-256…" : $"Загрузка обновления: {percent}%";
            });
            downloaded = await UpdateInstaller.DownloadAsync(asset, progress, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            StatusText.Text = "SHA-256 совпадает. Перезапускаю Observer…";
            CancelInstallButton.IsEnabled = false;
            SelfUpdate.StartHelper(downloaded);
            _installLaunching = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            if (!_installLaunching && downloaded != null) UpdateInstaller.TryDelete(Path.GetDirectoryName(downloaded.Path)!);
            StatusText.Text = ex is OperationCanceledException
                ? "Загрузка отменена или превышено время ожидания."
                : "Обновление не выполнено: " + ex.Message;
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
        try { _main.DismissAvailableUpdate(); StatusText.Text = "Уведомление об этой версии скрыто до появления новой."; DismissButton.Visibility = Visibility.Collapsed; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; }
    }
}
