using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace Observer.ControlCenter.Services;

/// <summary>Temporary copy of the current app waits for its parent, then replaces the single EXE.</summary>
public static class SelfUpdate
{
    public const string HelperArgument = "--observer-apply-update";
    private const string ExeName = "Observer.ControlCenter.exe";

    public static bool HasOriginalName(string? path) =>
        string.Equals(Path.GetFileName(path), ExeName, StringComparison.OrdinalIgnoreCase);

    public static void StartHelper(VerifiedUpdate update)
    {
        string target = Environment.ProcessPath ?? throw new InvalidOperationException(UiText.T("Current EXE not found.", "Не найден текущий EXE."));
        if (!HasOriginalName(target))
            throw new InvalidOperationException(UiText.T("Self-update requires the original filename Observer.ControlCenter.exe.", "Для автообновления нужен исходный файл Observer.ControlCenter.exe."));
        string folder = Path.GetDirectoryName(update.Path)!;
        string helperFolder = Path.Combine(folder, "helper");
        Directory.CreateDirectory(helperFolder);
        string helper = Path.Combine(helperFolder, ExeName);
        File.Copy(target, helper);
        using var current = Process.GetCurrentProcess();
        var info = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = helperFolder };
        foreach (string value in new[] { HelperArgument, current.Id.ToString(CultureInfo.InvariantCulture),
            current.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), target, update.Path, update.Sha256 })
            info.ArgumentList.Add(value);
        using var process = Process.Start(info) ?? throw new InvalidOperationException(UiText.T("Could not start the update helper.", "Не удалось запустить помощник обновления."));
    }

    public static void RunHelper(string[] args)
    {
        if (args.Length != 6 || args[0] != HelperArgument
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            || !long.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks))
            throw new InvalidDataException(UiText.T("Invalid update parameters.", "Некорректные параметры обновления."));
        string target = Path.GetFullPath(args[3]);
        string payload = Path.GetFullPath(args[4]);
        string hash = args[5];
        string folder = Path.GetDirectoryName(payload)!;
        string ownPath = Path.GetFullPath(Environment.ProcessPath ?? "");
        if (!string.Equals(Path.GetFileName(target), ExeName, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(payload) != ExeName
            || !string.Equals(ownPath, Path.Combine(folder, "helper", ExeName), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(folder), Path.TrimEndingDirectorySeparator(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(folder).StartsWith("ObserverUpdate-", StringComparison.Ordinal)
            || string.Equals(target, payload, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target, ownPath, StringComparison.OrdinalIgnoreCase)
            || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException(UiText.T("Invalid update path.", "Некорректный путь обновления."));
        Process? parent = null;
        try { parent = Process.GetProcessById(pid); }
        catch (ArgumentException) { }
        using (parent)
        {
            if (parent != null && !parent.HasExited && parent.StartTime.ToUniversalTime().Ticks == ticks
                && !parent.WaitForExit(30000))
                throw new IOException(UiText.T("Observer did not close. The old EXE was not changed.", "Observer не закрылся. Старый EXE не изменён."));
        }

        string stage = target + "." + Guid.NewGuid().ToString("N") + ".new";
        string backup = target + "." + Guid.NewGuid().ToString("N") + ".bak";
        bool replaced = false;
        try
        {
            if (!File.Exists(target)) throw new FileNotFoundException(UiText.T("Old EXE not found.", "Старый EXE не найден."), target);
            File.Copy(payload, stage, false);
            using (var stream = File.OpenRead(stage))
            {
                if (stream.Length <= 0 || stream.Length > UpdateInstaller.MaxInstallerBytes
                    || !UpdateInstaller.HashMatches(hash, SHA256.HashData(stream)))
                    throw new InvalidDataException(UiText.T("Second SHA-256 verification failed.", "Повторная проверка SHA-256 не пройдена."));
            }
            // Preserve Internet-zone metadata on the replacement. Do not disable Windows security.
            if (OperatingSystem.IsWindows())
                File.WriteAllText(stage + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            ReplaceWithBackup(stage, target, backup);
            replaced = true;
            if (OperatingSystem.IsWindows())
                File.WriteAllText(target + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            Launch(target, verifyStartup:true);
            // Retain the backup for manual recovery if the new app crashes after process creation.
        }
        catch (Exception ex)
        {
            try
            {
                if (replaced) RestoreBackup(backup, target);
                if (File.Exists(target)) Launch(target);
            }
            catch (Exception recovery)
            {
                throw new IOException((UiText.IsRussian ? $"Ошибка обновления: {ex.Message}\nОшибка восстановления: {recovery.Message}\nРезервная копия: {backup}" : $"Update error: {ex.Message}\nRecovery error: {recovery.Message}\nBackup: {backup}"), recovery);
            }
            throw new IOException(UiText.T("Update cancelled; previous file preserved: ", "Обновление отменено, прежний файл сохранён: ") + ex.Message, ex);
        }
        finally
        {
            try { File.Delete(stage); File.Delete(payload); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public static void ReplaceWithBackup(string stage, string target, string backup) => File.Replace(stage, target, backup);
    public static void RestoreBackup(string backup, string target) => File.Replace(backup, target, null);

    private static void Launch(string target, bool verifyStartup=false)
    {
        using var process = Process.Start(new ProcessStartInfo(target)
        { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target)! })
            ?? throw new InvalidOperationException(UiText.T("Could not start Observer.", "Не удалось запустить Observer."));
        if (verifyStartup && process.WaitForExit(5000) && process.ExitCode != 0)
            throw new IOException(UiText.T("Updated Observer exited during startup.","Обновлённый Observer завершился с ошибкой при запуске.") + $" ({process.ExitCode})");
    }
}
