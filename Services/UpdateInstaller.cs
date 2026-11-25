using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Observer.ControlCenter.Services;

public sealed record InstallerAsset(string Name, Uri DownloadUrl, Uri ChecksumUrl, long Size);

/// <summary>Opt-in installer download from the official repository only. Never launches a process.</summary>
public static class UpdateInstaller
{
    public const string OfficialRepository = "KingProjects12/Observer-Control-Center";
    public const long MaxInstallerBytes = 256L * 1024 * 1024;
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    { Timeout = Timeout.InfiniteTimeSpan };

    public static InstallerAsset? FindAsset(JsonElement root, string repository, string tag, ReleaseVersion version)
    {
        if (!string.Equals(repository, OfficialRepository, StringComparison.OrdinalIgnoreCase)
            || version.Prerelease.Length != 0 || tag != "v" + version
            || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        string name = $"Observer-Control-Center-Setup-{version}-win-x64.exe";
        string prefix = $"https://github.com/{OfficialRepository}/releases/download/{tag}/";
        long size = 0;
        bool hasChecksum = false;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object
                || !asset.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String
                || !asset.TryGetProperty("browser_download_url", out var u) || u.ValueKind != JsonValueKind.String) continue;
            string? assetName = n.GetString();
            if (u.GetString() != prefix + assetName) continue;
            if (assetName == name && asset.TryGetProperty("size", out var s)
                && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out long bytes)
                && bytes > 0 && bytes <= MaxInstallerBytes) size = bytes;
            if (assetName == name + ".sha256" && asset.TryGetProperty("size", out var h)
                && h.ValueKind == JsonValueKind.Number && h.TryGetInt64(out long hashSize)
                && hashSize > 0 && hashSize <= 1024) hasChecksum = true;
        }
        return size > 0 && hasChecksum
            ? new InstallerAsset(name, new Uri(prefix + name), new Uri(prefix + name + ".sha256"), size) : null;
    }

    public static string ParseChecksum(string text, string expectedName)
    {
        var parts = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0].Length != 64 || !parts[0].All(Uri.IsHexDigit)
            || parts[1] != expectedName) throw new InvalidDataException("Некорректный файл SHA-256.");
        return parts[0].ToUpperInvariant();
    }

    public static bool HashMatches(string expected, byte[] actual) =>
        string.Equals(expected, Convert.ToHexString(actual), StringComparison.OrdinalIgnoreCase);

    private static bool AllowedUri(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com"
            || uri.Host == "objects.githubusercontent.com");

    private static async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken ct)
    {
        for (int i = 0; i < 6; i++)
        {
            if (!AllowedUri(uri)) throw new InvalidDataException("Недопустимый адрес загрузки.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Observer-ControlCenter-Installer/1.0");
            var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location == null) throw new InvalidDataException("GitHub не указал адрес загрузки.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Слишком много перенаправлений загрузки.");
    }

    public static async Task<string> DownloadAsync(InstallerAsset asset, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        // Revalidate even if the caller supplies a constructed InstallerAsset.
        const string prefix = "https://github.com/" + OfficialRepository + "/releases/download/";
        string name = asset.Name;
        if (!name.StartsWith("Observer-Control-Center-Setup-", StringComparison.Ordinal)
            || !name.EndsWith("-win-x64.exe", StringComparison.Ordinal)
            || !ReleaseVersion.TryParse(name["Observer-Control-Center-Setup-".Length..^"-win-x64.exe".Length], out var version)
            || version.Prerelease.Length != 0
            || name != $"Observer-Control-Center-Setup-{version}-win-x64.exe"
            || asset.DownloadUrl.AbsoluteUri != prefix + "v" + version + "/" + name
            || asset.ChecksumUrl.AbsoluteUri != asset.DownloadUrl.AbsoluteUri + ".sha256"
            || asset.Size <= 0 || asset.Size > MaxInstallerBytes)
            throw new InvalidDataException("Установщик не соответствует официальному релизу.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        var ct = timeout.Token;
        string folder = Path.Combine(Path.GetTempPath(), "ObserverUpdate-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(folder, name);
        try
        {
            using var checksum = await GetAsync(asset.ChecksumUrl, ct).ConfigureAwait(false);
            using var checksumStream = await checksum.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var checksumBuffer = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await checksumStream.ReadAsync(buffer.AsMemory(0, 1025), ct).ConfigureAwait(false)) > 0)
            {
                if (checksumBuffer.Length + read > 1024) throw new InvalidDataException("Файл SHA-256 слишком большой.");
                checksumBuffer.Write(buffer, 0, read);
            }
            string expected = ParseChecksum(System.Text.Encoding.ASCII.GetString(checksumBuffer.ToArray()), name);
            Directory.CreateDirectory(folder);
            using var response = await GetAsync(asset.DownloadUrl, ct).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long length && length != asset.Size)
                throw new InvalidDataException("Размер установщика не совпадает с релизом.");
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            int lastProgress = -1;
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                while ((read = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > asset.Size) throw new InvalidDataException("Размер установщика превышает ожидаемый.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, read);
                    int percent = (int)(total * 100 / asset.Size);
                    if (percent != lastProgress) { progress?.Report(percent); lastProgress = percent; }
                }
            }
            if (total != asset.Size || !HashMatches(expected, hash.GetHashAndReset()))
                throw new InvalidDataException("Проверка размера или SHA-256 не пройдена. Установка отменена.");
            ct.ThrowIfCancellationRequested();
            // Preserve Windows Internet-zone marking; never bypass SmartScreen.
            if (OperatingSystem.IsWindows())
                await File.WriteAllTextAsync(destination + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n", ct).ConfigureAwait(false);
            return destination;
        }
        catch { TryDelete(folder); throw; }
    }

    public static void TryDelete(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
