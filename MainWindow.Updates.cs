using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Observer.ControlCenter.Services;

namespace Observer.ControlCenter;

/// <summary>Quiet checks while the app is running. A portable EXE is never replaced in place.</summary>
public partial class MainWindow
{
    private readonly UpdateSettings _updateSettings = UpdateSettingsStore.Load();
    private readonly CancellationTokenSource _updateShutdown = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private bool _updateCheckBusy;
    private GitHubRelease? _latestRelease;
    private UpdateCenterWindow? _updateCenter;
    private bool _openingUpdateCenter;
    private UpdateNoticeWindow? _updateNotice;
    private string _updateMessage = "Репозиторий обновлений ещё не задан.";

    internal string UpdatesRepository => _updateSettings.Repository;
    internal bool UpdatesAutoCheck => _updateSettings.AutoCheck;
    internal GitHubRelease? LatestUpdate => _latestRelease;
    internal string UpdateMessage => _updateMessage;

    private void StartUpdateChecks()
    {
        if (GitHubReleaseClient.IsValidRepository(_updateSettings.Repository))
            _updateMessage = "Источник обновлений: " + _updateSettings.Repository;
        _updateTimer.Tick += async (_, _) => await CheckUpdatesAsync(manual: false);
        _updateTimer.Start();
        _ = CheckShortlyAfterStartAsync();
        RefreshUpdateBanner();
    }

    private async Task CheckShortlyAfterStartAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(25), _updateShutdown.Token);
            await CheckUpdatesAsync(manual: false);
        }
        catch (OperationCanceledException) when (_updateShutdown.IsCancellationRequested) { }
    }

    internal async Task<GitHubRelease?> CheckUpdatesAsync(bool manual)
    {
        if (!manual && !_updateSettings.AutoCheck) return _latestRelease;
        if (!GitHubReleaseClient.IsValidRepository(_updateSettings.Repository))
        {
            _updateMessage = "Сначала укажи owner/repo в настройках обновлений.";
            if (manual) AddLog("UPDATES  //  GitHub репозиторий не настроен");
            return null;
        }
        if (_updateCheckBusy) return _latestRelease;
        _updateCheckBusy = true;
        try
        {
            var discovered = await GitHubReleaseClient.CheckAsync(_updateSettings.Repository, _updateShutdown.Token);
            _latestRelease = discovered;
            if (discovered == null)
            {
                _updateMessage = $"Установлена актуальная версия: {GitHubReleaseClient.AppVersionText}";
                if (manual) AddLog("UPDATES  //  опубликованная версия не новее установленной");
            }
            else
            {
                _updateMessage = $"Новая версия {discovered.Tag} доступна на GitHub";
                bool wasShown = _shownUpdateTag == discovered.Tag;
                if (manual) AddLog($"UPDATES  //  доступна {discovered.Tag}");
                if (!wasShown && _updateSettings.DismissedTag != discovered.Tag)
                {
                    _shownUpdateTag = discovered.Tag;
                    AddLog($"NEW RELEASE  //  {discovered.Tag} · нажми ОБНОВЛЕНИЯ");
                    UpdateToastText.Text = $"Observer {discovered.Tag} доступен. Посмотреть изменения?";
                    UpdateToast.Visibility = Visibility.Visible;
                    ShowUpdateNotice(discovered);
                }
            }
        }
        catch (OperationCanceledException) when (_updateShutdown.IsCancellationRequested) { }
        catch (HttpRequestException e)
        {
            _updateMessage = e.StatusCode == HttpStatusCode.NotFound
                ? "GitHub не нашёл опубликованный стабильный Release (HTTP 404)."
                : e.StatusCode == HttpStatusCode.Forbidden || (int?)e.StatusCode == 429
                ? "GitHub отклонил запрос или исчерпан лимит проверок. Повтори позже."
                : "GitHub временно недоступен. Проверь сеть и повтори позже.";
            if (manual) AddLog($"UPDATES ERROR  //  HTTP {e.StatusCode?.ToString() ?? "network"}");
        }
        catch (Exception e) when (e is ArgumentException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _updateMessage = e is TaskCanceledException ? "Превышено время ответа GitHub."
                : "Не удалось прочитать сведения о последнем релизе.";
            if (manual) AddLog("UPDATES ERROR  //  " + e.GetType().Name);
        }
        finally
        {
            _updateCheckBusy = false;
            RefreshUpdateBanner();
        }
        return _latestRelease;
    }

    private string _shownUpdateTag = "";

    private void RefreshUpdateBanner()
    {
        if (BtnUpdates == null) return;
        bool show = _latestRelease != null && _latestRelease.Tag != _updateSettings.DismissedTag;
        if (!show) UpdateToast.Visibility = Visibility.Collapsed;
        BtnUpdates.Content = show ? $"●  НОВАЯ {_latestRelease!.Tag}" : "↻  ОБНОВЛЕНИЯ";
        BtnUpdates.ToolTip = show ? "Открыть сведения о новой версии и перейти на GitHub Releases"
            : "Проверить и настроить обновления GitHub";
        if (_island != null)
        {
            if (show) _island.SetUpdateAvailable(_latestRelease!.Tag);
            else _island.ClearUpdateNotice();
        }
    }

    internal void SaveUpdateSettings(string repo, bool autoCheck)
    {
        repo = repo.Trim();
        if (!GitHubReleaseClient.IsValidRepository(repo))
            throw new ArgumentException("Укажи GitHub репозиторий в формате владелец/проект, например example/observer.");
        if (_updateSettings.Repository != repo)
        {
            _updateSettings.DismissedTag = "";
            _latestRelease = null;
            _shownUpdateTag = "";
            UpdateToast.Visibility = Visibility.Collapsed;
            _updateNotice?.Close();
        }
        string locked = UpdateSettingsStore.EmbeddedRepository;
        if (GitHubReleaseClient.IsValidRepository(locked) &&
            !string.Equals(repo, locked, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Источник обновлений официальной сборки зафиксирован при публикации.");
        _updateSettings.Repository = GitHubReleaseClient.IsValidRepository(locked) ? locked : repo;
        _updateSettings.AutoCheck = autoCheck;
        UpdateSettingsStore.Save(_updateSettings);
        _updateMessage = "Настройки обновлений сохранены локально.";
        AddLog($"UPDATES  //  GitHub repository configured; auto-check={(autoCheck ? "ON" : "OFF")}");
        RefreshUpdateBanner();
    }

    internal void DismissAvailableUpdate()
    {
        if (_latestRelease == null) return;
        _updateSettings.DismissedTag = _latestRelease.Tag;
        UpdateSettingsStore.Save(_updateSettings);
        RefreshUpdateBanner();
    }

    internal void OpenReleasePage()
    {
        if (!GitHubReleaseClient.IsValidRepository(_updateSettings.Repository)) return;
        var uri = GitHubReleaseClient.ReleasesPage(_updateSettings.Repository);
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    private void DismissToast_Click(object sender, RoutedEventArgs e)
    {
        UpdateToast.Visibility = Visibility.Collapsed;
        // The header indicator remains; this only closes the temporary pop-up.
    }

    private void ShowUpdateNotice(GitHubRelease release)
    {
        // One passive, clickable desktop notification. No installer or arbitrary executable is launched.
        _updateNotice?.Close();
        _updateNotice = new UpdateNoticeWindow(release.Tag, () => ShowUpdateCenter());
        _updateNotice.Closed += (_, _) => _updateNotice = null;
        _updateNotice.Show();
    }

    internal async void ShowUpdateCenter()
    {
        if (_openingUpdateCenter)
        {
            _updateCenter?.Activate();
            return;
        }
        _openingUpdateCenter = true;
        try
        {
            UpdateToast.Visibility = Visibility.Collapsed;
            _updateNotice?.Close();
            if (_island is { IsVisible: true })
                await RestoreFromIslandAsync();
            if (!IsVisible || WindowState == WindowState.Minimized)
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = 0;
                Show();
                WindowState = WindowState.Maximized;
                await WindowMotion.FadeAsync(this, 1, 180);
            }
            Activate();
            if (_updateCenter is { IsLoaded: true })
            {
                _updateCenter.Activate();
                return;
            }
            _updateCenter = new UpdateCenterWindow(this) { Owner = this };
            try { _updateCenter.ShowDialog(); }
            finally
            {
                _updateCenter = null;
                RefreshUpdateBanner();
            }
        }
        finally { _openingUpdateCenter = false; }
    }

    private void Updates_Click(object sender, RoutedEventArgs e) => ShowUpdateCenter();
}
