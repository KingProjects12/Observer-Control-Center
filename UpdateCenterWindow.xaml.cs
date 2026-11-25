using System.Windows;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

public partial class UpdateCenterWindow : Window
{
    private readonly MainWindow _main;

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

    private void RefreshInfo()
    {
        StatusText.Text = _main.UpdateMessage;
        var release = _main.LatestUpdate;
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
        StatusText.Text = "Проверяю GitHub Releases…";
        try { await _main.CheckUpdatesAsync(manual: true); }
        finally { CheckButton.IsEnabled = true; RefreshInfo(); }
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
