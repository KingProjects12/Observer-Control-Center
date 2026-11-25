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
    private readonly DispatcherTimer _updateTimer = new() { Interval = UpdatePollingPolicy.TestInterval };
    private DateTimeOffset _updatesBlockedUntilUtc = DateTimeOffset.MinValue;
    private bool _updateCheckBusy;
    private GitHubRelease? _latestRelease;
    private UpdateCenterWindow? _updateCenter;
    private bool _openingUpdateCenter;
    private UpdateNoticeWindow? _updateNotice;
    private string _updateMessage = UiText.T("Update repository has not been configured.", "Репозиторий обновлений ещё не задан.");

    internal string UpdatesRepository => _updateSettings.Repository;
    internal bool UpdatesAutoCheck => _updateSettings.AutoCheck;
    internal GitHubRelease? LatestUpdate => _latestRelease;
    internal string UpdateMessage => _updateMessage;

    private void StartUpdateChecks()
    {
        if (GitHubReleaseClient.IsValidRepository(_updateSettings.Repository))
            _updateMessage = UiText.T("Update source: ", "Источник обновлений: ") + _updateSettings.Repository;
        _updateTimer.Tick += async (_, _) => await CheckUpdatesAsync(manual: false);
        // Start the timer after the initial delayed check to avoid two requests in the first minute.
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
        finally
        {
            if (!_updateShutdown.IsCancellationRequested)
                _updateTimer.Start();
        }
    }

    internal async Task<GitHubRelease?> CheckUpdatesAsync(bool manual)
    {
        if (!manual && !_updateSettings.AutoCheck) return _latestRelease;
        if (!GitHubReleaseClient.IsValidRepository(_updateSettings.Repository))
        {
            _updateMessage = UiText.T("Set owner/repo in update settings first.", "Сначала укажи owner/repo в настройках обновлений.");
            if (manual) AddLog(UiText.T("UPDATES  //  GitHub repository not configured", "UPDATES  //  GitHub репозиторий не настроен"));
            return null;
        }
        if (_updateCheckBusy) return _latestRelease;
        if (UpdatePollingPolicy.IsThrottled(DateTimeOffset.UtcNow, _updatesBlockedUntilUtc))
        {
            _updateMessage = (UiText.IsRussian ? $"GitHub временно ограничил запросы. Следующая попытка после {_updatesBlockedUntilUtc.ToLocalTime():HH:mm}." : $"GitHub temporarily limited requests. Next attempt after {_updatesBlockedUntilUtc.ToLocalTime():HH:mm}.");
            if (manual) AddLog(UiText.T("UPDATES  //  GitHub API rate-limit cooldown is active", "UPDATES  //  действует пауза из-за лимита GitHub API"));
            RefreshUpdateBanner();
            return _latestRelease;
        }
        _updateCheckBusy = true;
        try
        {
            var discovered = await GitHubReleaseClient.CheckAsync(_updateSettings.Repository, _updateShutdown.Token);
            _latestRelease = discovered;
            if (discovered == null)
            {
                _updateMessage = (UiText.IsRussian ? $"Установлена актуальная версия: {GitHubReleaseClient.AppVersionText}" : $"Installed version is up to date: {GitHubReleaseClient.AppVersionText}");
                if (manual) AddLog(UiText.T("UPDATES  //  no newer published version", "UPDATES  //  опубликованная версия не новее установленной"));
            }
            else
            {
                _updateMessage = (UiText.IsRussian ? $"Новая версия {discovered.Tag} доступна на GitHub" : $"New version {discovered.Tag} is available on GitHub");
                bool wasShown = _shownUpdateTag == discovered.Tag;
                if (manual) AddLog((UiText.IsRussian ? $"UPDATES  //  доступна {discovered.Tag}" : $"UPDATES  //  {discovered.Tag} available"));
                if (!wasShown && _updateSettings.DismissedTag != discovered.Tag)
                {
                    _shownUpdateTag = discovered.Tag;
                    AddLog((UiText.IsRussian ? $"NEW RELEASE  //  {discovered.Tag} · нажми ОБНОВЛЕНИЯ" : $"NEW RELEASE  //  {discovered.Tag} · click UPDATES"));
                    UpdateToastText.Text = (UiText.IsRussian ? $"Observer {discovered.Tag} доступен. Посмотреть изменения?" : $"Observer {discovered.Tag} is available. View changes?");
                    UpdateToast.Visibility = Visibility.Visible;
                    if (SystemParameters.ClientAreaAnimation)
                    {
                        UpdateToast.BeginAnimation(OpacityProperty, null);
                        UpdateToast.Opacity = 1;
                        UpdateToast.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0.0, To = 1.0,
                            Duration = TimeSpan.FromMilliseconds(210),
                            FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
                        });
                    }
                    ShowUpdateNotice(discovered);
                }
            }
        }
        catch (OperationCanceledException) when (_updateShutdown.IsCancellationRequested) { }
        catch (HttpRequestException e)
        {
            // The public GitHub API may limit unauthenticated clients to ~60 requests/hour.
            // Poll every minute during testing, but stop if GitHub returns HTTP 403 or 429.
            bool limited = e.StatusCode == HttpStatusCode.Forbidden || (int?)e.StatusCode == 429;
            if (limited)
                _updatesBlockedUntilUtc = DateTimeOffset.UtcNow + UpdatePollingPolicy.RateLimitCooldown;
            _updateMessage = e.StatusCode == HttpStatusCode.NotFound
                ? UiText.T("GitHub found no published stable release (HTTP 404).", "GitHub не нашёл опубликованный стабильный Release (HTTP 404).")
                : e.StatusCode == HttpStatusCode.Forbidden || (int?)e.StatusCode == 429
                ? UiText.T("GitHub rejected the request or rate limit was reached. Automatic checks paused for 30 minutes.", "GitHub отклонил запрос или исчерпан лимит. Автоматические проверки приостановлены на 30 минут.")
                : UiText.T("GitHub is temporarily unavailable. Check your connection and retry.", "GitHub временно недоступен. Проверь сеть и повтори позже.");
            if (manual) AddLog($"UPDATES ERROR  //  HTTP {e.StatusCode?.ToString() ?? "network"}");
        }
        catch (Exception e) when (e is ArgumentException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _updateMessage = e is TaskCanceledException ? UiText.T("GitHub response timed out.", "Превышено время ответа GitHub.")
                : UiText.T("Could not read the latest release information.", "Не удалось прочитать сведения о последнем релизе.");
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
        _updateCenter?.RefreshInfo();
        bool show = _latestRelease != null && _latestRelease.Tag != _updateSettings.DismissedTag;
        if (!show) UpdateToast.Visibility = Visibility.Collapsed;
        BtnUpdates.Content = show ? (UiText.IsRussian ? $"●  НОВАЯ {_latestRelease!.Tag}" : $"●  NEW {_latestRelease!.Tag}") : UiText.T("↻  UPDATES", "↻  ОБНОВЛЕНИЯ");
        BtnUpdates.ToolTip = show ? UiText.T("View new release and update", "Открыть сведения о новой версии и установить обновление")
            : UiText.T("Check and configure GitHub updates", "Проверить и настроить обновления GitHub");
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
            throw new ArgumentException(UiText.T("Enter a GitHub repository as owner/repo, e.g. example/observer.", "Укажи GitHub репозиторий в формате владелец/проект, например example/observer."));
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
            throw new ArgumentException(UiText.T("The official build has a fixed update source.", "Источник обновлений официальной сборки зафиксирован при публикации."));
        _updateSettings.Repository = GitHubReleaseClient.IsValidRepository(locked) ? locked : repo;
        _updateSettings.AutoCheck = autoCheck;
        UpdateSettingsStore.Save(_updateSettings);
        _updateMessage = UiText.T("Update settings saved locally.", "Настройки обновлений сохранены локально.");
        AddLog((UiText.IsRussian ? $"UPDATES  //  репозиторий настроен; автопроверка={(autoCheck ? "ВКЛ" : "ВЫКЛ")}" : $"UPDATES  //  GitHub repository configured; auto-check={(autoCheck ? "ON" : "OFF")}"));
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

    private void Updates_Click(object sender, RoutedEventArgs e)
    {
        using var selected=sender is System.Windows.Controls.MenuItem ? BeginNavigationAction(NavTools) : null;
        ShowUpdateCenter();
    }
}
