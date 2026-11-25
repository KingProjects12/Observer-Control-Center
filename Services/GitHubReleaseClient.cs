using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Observer.ControlCenter.Services;

public sealed record GitHubRelease(string Tag, ReleaseVersion Version, string Notes, string ReleaseUrl, DateTimeOffset? PublishedAt, InstallerAsset? Installer = null);

/// <summary>Read-only public GitHub Releases check. No GitHub tokens, cookies, or router credentials.</summary>
public static class GitHubReleaseClient
{
    private static readonly Regex RepoPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HttpClient Http = CreateHttp();

    public static string AppVersionText => typeof(GitHubReleaseClient).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?.Split('+')[0] ?? "2.0.0-preview.1";

    public static ReleaseVersion CurrentVersion => ReleaseVersion.TryParse(AppVersionText, out var v)
        ? v : new ReleaseVersion(2, 0, 2, "preview.1");

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Observer-ControlCenter-UpdateCheck/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public static bool IsValidRepository(string? repository) => !string.IsNullOrWhiteSpace(repository)
        && RepoPattern.IsMatch(repository) && !repository.Split('/').Any(x => x is "." or "..");

    public static string ReleasesPage(string repository)
    {
        if (!IsValidRepository(repository)) throw new ArgumentException(UiText.T("Enter a GitHub repository as owner/repo.", "Укажи GitHub репозиторий в формате owner/repo."), nameof(repository));
        return $"https://github.com/{repository}/releases/latest";
    }

    public static GitHubRelease? ParseLatest(string json, string repository, ReleaseVersion current)
    {
        if (!IsValidRepository(repository)) throw new ArgumentException(UiText.T("Invalid GitHub repository.", "Некорректный GitHub репозиторий."), nameof(repository));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
        string tag = root.TryGetProperty("tag_name", out var tagNode) && tagNode.ValueKind == JsonValueKind.String
            ? tagNode.GetString() ?? "" : "";
        if (!ReleaseVersion.TryParse(tag, out var latest) || latest.CompareTo(current) <= 0) return null;
        string notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
            ? body.GetString() ?? "" : "";
        if (notes.Length > 12000) notes = notes[..12000] + "\n…";
        DateTimeOffset? date = null;
        if (root.TryGetProperty("published_at", out var pub) && pub.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(pub.GetString(), out var parsedDate)) date = parsedDate;
        // Derive the link from a validated repository instead of trusting a supplied URL.
        return new GitHubRelease(tag, latest, notes, ReleasesPage(repository), date, UpdateInstaller.FindAsset(root, repository, tag, latest));
    }

    public static async Task<GitHubRelease?> CheckAsync(string repository, CancellationToken ct = default)
    {
        if (!IsValidRepository(repository)) throw new ArgumentException(UiText.T("Enter a GitHub repository as owner/repo.", "Укажи GitHub репозиторий в формате owner/repo."), nameof(repository));
        string url = $"https://api.github.com/repos/{repository}/releases/latest";
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(ct);
        return ParseLatest(body, repository, CurrentVersion);
    }
}
