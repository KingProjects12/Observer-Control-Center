using Microsoft.Win32;
using System.Globalization;
namespace Observer.ControlCenter.Services;

public static class UiText
{
    public static bool IsRussian { get; private set; }
    public static bool ResolveRussian(string? language) => string.Equals(language, "russian", StringComparison.OrdinalIgnoreCase)
        || string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) || string.Equals(language, "ru-RU", StringComparison.OrdinalIgnoreCase);
    public static void Initialize()
    {
        string? language = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\KingProjects12\ObserverControlCenter");
                language = key?.GetValue("Language") as string;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        }
        IsRussian = ResolveRussian(language);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(IsRussian ? "ru-RU" : "en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;
    }
    public static string KeeneticConnect => T("◉   Connect / NAT", "◉   Подключение / NAT");
    public static string LegacyConnect => T("CONNECT", "ПОДКЛЮЧИТЬСЯ");
    public static string LegacyVpn => T("◈   VPN REMOTE", "◈   VPN REMOTE");
    public static string LegacyTraffic => T("◫   Collect traffic", "◫   Собрать трафик");
    public static string LegacyTools => T("☰   Tools ▾", "☰   Инструменты ▾");
    public static string LegacyHint => T("Enter router credentials in Connect / NAT. Reports stay on this computer.", "Введите логин и пароль в «Подключение / NAT». Отчёты остаются на этом компьютере.");
    public static string T(string english, string russian) => IsRussian ? russian : english;
    public static string SelectNotes(string notes, bool russian)
    {
        const string en = "<!-- observer:en -->", ru = "<!-- observer:ru -->";
        int a = notes.IndexOf(en, StringComparison.Ordinal), b = notes.IndexOf(ru, StringComparison.Ordinal);
        return a >= 0 && b > a ? (russian ? notes[(b + ru.Length)..] : notes[(a + en.Length)..b]).Trim() : notes;
    }
    public static string X001 => T("Observer Control Center — Preview", "Observer Control Center — Предпросмотр");
    public static string X002 => T("  //  CONTROL CENTER", "  //  ЦЕНТР УПРАВЛЕНИЯ");
    public static string X003 => T("  BLACK GLASS", "  ЧЁРНОЕ СТЕКЛО");
    public static string X004 => T("WHO  →  WHERE  →  WHEN      /      NETWORK ANALYTICS", "КТО  →  КУДА  →  КОГДА      /      СЕТЕВАЯ АНАЛИТИКА");
    public static string X005 => T("●  LOCAL · READ-ONLY", "●  ЛОКАЛЬНО · ТОЛЬКО ЧТЕНИЕ");
    public static string X006 => T("↻  UPDATES", "↻  ОБНОВЛЕНИЯ");
    public static string X007 => T("◔  ISLAND", "◔  ОСТРОВОК");
    public static string X008 => T("PROJECT / 01", "ПРОЕКТ / 01");
    public static string X009 => T("Network Analytics", "Сетевая аналитика");
    public static string X010 => T("Local preview", "Локальный предпросмотр");
    public static string X011 => T("2026  •  CONTROL CENTER", "2026  •  ЦЕНТР УПРАВЛЕНИЯ");
    public static string X012 => T("WORKSPACE", "РАБОЧАЯ ПАНЕЛЬ");
    public static string X013 => T("⌂   Overview", "⌂   Обзор");
    public static string X014 => T("◫   TOP Traffic", "◫   ТОП трафика");
    public static string X015 => T("⤳   Connections", "⤳   Соединения");
    public static string X016 => T("▤   Reports", "▤   Отчёты");
    public static string X017 => T("↻   GitHub · Updates", "↻   GitHub · Обновления");
    public static string X018 => T("DIAGNOSTICS", "ДИАГНОСТИКА");
    public static string X019 => T("◉   Check address", "◉   Проверить адрес");
    public static string X020 => T("＋   Load CSV", "＋   Загрузить CSV");
    public static string X021 => T("◌   Change accent", "◌   Сменить акцент");
    public static string X022 => T("✦   Glass: TRANSLUCENT", "✦   Стекло: ПРОЗРАЧНО");
    public static string X023 => T("Switch glass density: translucent, maximum, matte", "Переключить уровень прозрачности: прозрачно, максимум, матово");
    public static string X024 => T("CURRENT VERSION", "ТЕКУЩАЯ ВЕРСИЯ");
    public static string X025 => T("✓ CSV / tables / filters", "✓ CSV / таблицы / фильтры");
    public static string X026 => T("✓ HTTP/HTTPS TCP check", "✓ HTTP/HTTPS проверка TCP");
    public static string X027 => T("✓ NAT API / ○ TOP Traffic: planned", "✓ NAT API / ○ TOP Traffic: позже");
    public static string X028 => T("NETWORK OVERVIEW", "ОБЗОР СЕТИ");
    public static string X029 => T("Connect to Keenetic or open a local report", "Подключитесь к Keenetic или откройте локальный отчёт");
    public static string X030 => T("Device IP", "IP устройства");
    public static string X031 => T("Managed router address. Connect to enter credentials and collect data.", "Адрес роутера. Нажмите «Подключиться», чтобы ввести данные для входа и собрать отчёт.");
    public static string X032 => T("CHECK TCP", "ПРОВЕРИТЬ TCP");
    public static string X033 => T("DEVICES", "УСТРОЙСТВА");
    public static string X034 => T("TRAFFIC / WINDOW", "ТРАФИК / ОКНО");
    public static string X035 => T("ACTIVE DEVICES", "УЧТЕНО АКТИВНЫХ");
    public static string X036 => T("SOURCE", "ИСТОЧНИК");
    public static string X037 => T("TOP CONSUMERS", "ТОП ПОТРЕБИТЕЛЕЙ");
    public static string X038 => T("Ranking from a local Keenetic report", "Рейтинг из локального отчёта Keenetic");
    public static string X039 => T("Filter by IP, device name or MAC", "Фильтр по IP, имени устройства или MAC");
    public static string X040 => T("LOAD CSV", "ЗАГРУЗИТЬ CSV");
    public static string X041 => T("Device", "Устройство");
    public static string X042 => T("Download", "Получено");
    public static string X043 => T("Upload", "Отправлено");
    public static string X044 => T("Total", "Всего");
    public static string X045 => T("TOP / TRAFFIC PROFILE", "TOP / ПРОФИЛЬ ТРАФИКА");
    public static string X046 => T("By volume, not speed", "По объёму, не по скорости");
    public static string X047 => T("Statistics reflect available sources only. Local/VPN flows may be missing.", "Статистика только из доступных источников. Локальные/VPN-потоки могут отсутствовать.");
    public static string X048 => T("EVENTS / LOG", "СОБЫТИЯ / ЖУРНАЛ");
    public static string X049 => T("Select text and press Ctrl+C", "Выдели текст и нажми Ctrl+C");
    public static string X050 => T("⧉  COPY ALL", "⧉  КОПИРОВАТЬ ВСЁ");
    public static string X051 => T("Copy all log lines with timestamps", "Скопировать все строки журнала с отметками времени");
    public static string X052 => T("●  NEW OBSERVER VERSION", "●  НОВАЯ ВЕРСИЯ OBSERVER");
    public static string X053 => T("New version available", "Доступна новая версия");
    public static string X054 => T("VIEW UPDATE", "СМОТРЕТЬ ОБНОВЛЕНИЕ");
    public static string X055 => T("LATER", "ПОЗЖЕ");
    public static string X056 => T("READY // Open a v1.9.1 export or import CSV", "READY // Откройте экспорт v1.9.1 или импортируйте CSV");
    public static string X057 => T("OBSERVER  •  AUTHORIZED NETWORKS ONLY", "OBSERVER  •  ТОЛЬКО РАЗРЕШЁННЫЕ СЕТИ");
    public static string X058 => T("Resize window", "Изменить размер окна");
    public static string X059 => T("Observer · Updates", "Observer · Обновления");
    public static string X060 => T("OBSERVER   //   RELEASE CENTER", "OBSERVER   //   ЦЕНТР ОБНОВЛЕНИЙ");
    public static string X061 => T("UPDATE SOURCE · GitHub Releases", "ИСТОЧНИК ОБНОВЛЕНИЙ · GitHub Releases");
    public static string X062 => T("owner/repo, e.g. example/observer", "owner/repo, например example/observer");
    public static string X063 => T("Check at startup and every minute (test mode)", "Проверять при запуске и каждую минуту (тестовый режим)");
    public static string X064 => T("SAVE", "СОХРАНИТЬ");
    public static string X065 => T("Checking for updates", "Проверка обновлений");
    public static string X066 => T("Updates are checked while Observer is running. EXE replacement requires confirmation.", "Обновления проверяются только при запущенной программе. EXE заменяется только по твоему подтверждению.");
    public static string X067 => T("↻  CHECK NOW", "↻  ПРОВЕРИТЬ СЕЙЧАС");
    public static string X068 => T("UPDATE AND RESTART", "ОБНОВИТЬ И ПЕРЕЗАПУСТИТЬ");
    public static string X069 => T("CANCEL", "ОТМЕНА");
    public static string X070 => T("OPEN GITHUB RELEASES", "ОТКРЫТЬ GITHUB RELEASES");
    public static string X071 => T("SKIP THIS VERSION", "НЕ НАПОМИНАТЬ ОБ ЭТОЙ ВЕРСИИ");
    public static string X072 => T("WHAT'S NEW", "ЧТО НОВОГО");
    public static string X073 => T("Release notes will appear after checking.", "Список изменений появится после проверки.");
    public static string X074 => T("SHA-256 is verified. The new EXE replaces the current file without a setup wizard. A backup allows recovery. Code signing is not yet available.", "Проверяется SHA-256. Новый EXE заменяет текущий файл без мастера установки. При ошибке запуска сохраняется возможность возврата к старой версии. Цифровая подпись пока отсутствует.");
    public static string X075 => T("Observer — update", "Observer — обновление");
    public static string X076 => T("Close notification", "Закрыть уведомление");
    public static string X077 => T("OBSERVER  /  UPDATE", "OBSERVER  /  ОБНОВЛЕНИЕ");
    public static string X078 => T("New version available", "Доступна новая версия");
    public static string X079 => T("VIEW CHANGES", "ПОСМОТРЕТЬ ИЗМЕНЕНИЯ");
    public static string X080 => T("LATER", "ПОЗЖЕ");
    public static string X081 => T("Observer · Mini Island", "Observer · Мини-островок");
    public static string X082 => T("Local preview", "Локальный просмотр");
    public static string X083 => T("OPEN", "ОТКРЫТЬ");
}
