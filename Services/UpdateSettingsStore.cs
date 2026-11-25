using System.Reflection;
using System.Text.Json;

namespace Observer.ControlCenter.Services;

public sealed class UpdateSettings
{
    public string Repository { get; set; } = "";
    public bool AutoCheck { get; set; } = true;
    public string DismissedTag { get; set; } = "";
}

public static class UpdateSettingsStore
{
    public static string PathOnDisk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ObserverControlCenter", "update-settings.json");

    public static string EmbeddedRepository => typeof(UpdateSettingsStore).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(x => x.Key == "ObserverUpdateRepo")?.Value ?? "";

    // A public build cannot be redirected to somebody else's releases via a leftover local setting.
    public static string ResolveRepository(string? saved, string? embedded) =>
        GitHubReleaseClient.IsValidRepository(embedded) ? embedded! :
        GitHubReleaseClient.IsValidRepository(saved) ? saved! : "";

    public static UpdateSettings Load()
    {
        UpdateSettings settings;
        try
        {
            settings = File.Exists(PathOnDisk)
                ? JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(PathOnDisk)) ?? new()
                : new UpdateSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { settings = new UpdateSettings(); }
        settings.Repository = ResolveRepository(settings.Repository, EmbeddedRepository);
        settings.DismissedTag ??= "";
        return settings;
    }

    public static void Save(UpdateSettings settings)
    {
        if (settings.Repository.Length > 0 && !GitHubReleaseClient.IsValidRepository(settings.Repository))
            throw new ArgumentException(UiText.T("Repository format: owner/repo.", "Формат репозитория: owner/repo."));
        settings.Repository = ResolveRepository(settings.Repository, EmbeddedRepository);
        var folder = Path.GetDirectoryName(PathOnDisk)!;
        Directory.CreateDirectory(folder);
        var temporary = PathOnDisk + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, PathOnDisk, true);
    }
}
