using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Observer.ControlCenter.Services;

public sealed record NatRow(string Timestamp, int Sample, string Router, string SourceName, string SourceIP, string SourceMAC,
    string SourcePort, string DestinationIP, string DestinationPort, string Protocol, string Packets, string Bytes,
    string TranslatedSource, string TranslatedDestination, string TranslatedSourcePort, string TranslatedDestinationPort,
    string TargetMatch, string OfficeDirection, string OfficeFields);
public sealed record NatResult(IReadOnlyList<NatRow> Rows, int Skipped, bool HostnamesUnavailable);
public sealed record NatReport(string AllPath, string TargetPath, int AllCount, int TargetCount, int RelatedCount, int Skipped, bool HostnamesUnavailable);

/// <summary>Read-only Keenetic x-ndw2 login and NAT collection ported from Observer v1.9.1.
/// No configuration writes. No redirects, certificate bypass, proxies or stored credentials.</summary>
public static class KeeneticNatCollector
{
    private static string T(string en, string ru) => UiText.T(en, ru);
    private static readonly string[] Src = ["src", "source", "source-ip", "source_ip", "src-ip"];
    private static readonly string[] Dst = ["dst", "destination", "destination-ip", "destination_ip", "dst-ip"];
    private const int MaxResponse = 50_000_000;
    private static bool IPv4(string s, out IPAddress ip)
    {
        ip = IPAddress.None;
        if (s.Split('.').Length != 4 || s.Split('.').Any(x => x.Length == 0 || x.Any(c => c < '0' || c > '9'))) return false;
        if (!IPAddress.TryParse(s, out var p) || p.AddressFamily != AddressFamily.InterNetwork || p.ToString() != s) return false;
        ip = p;
        return true;
    }

    public static Uri RouterUri(string address, bool https)
    {
        var parts = address.Trim().Split(':');
        if (parts.Length is < 1 or > 2 || !IPv4(parts[0], out var ip) || ip.GetAddressBytes()[0] is 0 or 127 or >= 224)
            throw new ArgumentException(T("Enter a managed IPv4 address, optionally with a port.", "Введите IPv4 управляемого роутера, при необходимости с портом."));
        int port = https ? 443 : 80;
        if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
            throw new ArgumentException(T("Invalid port.", "Некорректный порт."));
        return new UriBuilder(https ? "https" : "http", ip.ToString(), port, "/").Uri;
    }

    public static (uint Network, uint Mask) ParseTarget(string value)
    {
        var parts = value.Trim().Split('/');
        if (parts.Length != 2 || !IPv4(parts[0], out var ip) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix) || prefix is < 1 or > 32)
            throw new ArgumentException(T("Enter the target IPv4 network in CIDR notation.", "Введите целевую сеть IPv4 в формате CIDR."));
        uint mask = uint.MaxValue << (32 - prefix);
        uint address = Number(ip);
        if ((address & mask) != address)
            throw new ArgumentException(T("CIDR must start at the network address.", "CIDR должен начинаться с адреса сети."));
        return (address, mask);
    }

    private static uint Number(IPAddress ip) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(ip.GetAddressBytes());
    public static bool Matches(string address, (uint Network, uint Mask) target) =>
        IPv4(address, out var ip) && (Number(ip) & target.Mask) == target.Network;

    public static string ChallengeHash(string username, string realm, string password, string challenge)
    {
        // Required by Keenetic x-ndw2 protocol; MD5 here is not used for local password storage.
        string md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(username + ":" + realm + ":" + password))).ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(challenge + md5))).ToLowerInvariant();
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "";

    private static async Task Authenticate(HttpClient client, string user, string password, CancellationToken ct, IProgress<string>? progress)
    {
        using var challenge = await client.GetAsync("/auth", HttpCompletionOption.ResponseHeadersRead, ct);
        progress?.Report("AUTH GET: HTTP " + (int)challenge.StatusCode);
        if (challenge.StatusCode == HttpStatusCode.OK) return; // RCI must succeed before connection is reported.
        if (challenge.StatusCode != HttpStatusCode.Unauthorized)
            throw new HttpRequestException(T("Keenetic authentication endpoint returned HTTP ", "Точка авторизации Keenetic вернула HTTP ") + (int)challenge.StatusCode);
        var realm = Header(challenge, "X-NDM-Realm");
        var nonce = Header(challenge, "X-NDM-Challenge");
        if (Header(challenge, "WWW-Authenticate").Contains("x-ndw4", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(nonce))
            throw new NotSupportedException(T("Unsupported authentication scheme; x-ndw2 required.", "Неподдерживаемая авторизация; требуется x-ndw2."));
        using var body = new StringContent(JsonSerializer.Serialize(new { login = user, password = ChallengeHash(user, realm, password, nonce) }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/auth", body, ct);
        progress?.Report("AUTH POST: HTTP " + (int)response.StatusCode);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new UnauthorizedAccessException(T("Login failed (HTTP ", "Ошибка входа (HTTP ") + (int)response.StatusCode + ").");
    }

    private static async Task<string> Get(HttpClient client, string path, CancellationToken ct, IProgress<string>? progress)
    {
        using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
        progress?.Report("GET " + path + ": HTTP " + (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(T("Keenetic API returned HTTP ", "Keenetic API вернул HTTP ") + (int)response.StatusCode);
        if (response.Content.Headers.ContentLength > MaxResponse) throw new InvalidDataException(T("Response exceeds limit.", "Ответ превышает лимит."));
        using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        byte[] buffer = new byte[65536];
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) != 0)
        {
            if (output.Length + read > MaxResponse) throw new InvalidDataException(T("Response exceeds limit.", "Ответ превышает лимит."));
            output.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');
    }

    private static string Field(JsonElement row, params string[] keys)
    {
        if (row.ValueKind != JsonValueKind.Object) return "";
        foreach (string key in keys)
            foreach (var p in row.EnumerateObject())
                if (p.Name.Equals(key, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    return p.Value.ToString();
        return "";
    }

    private static IEnumerable<JsonElement> Records(JsonElement node, bool hosts, int depth = 0)
    {
        if (depth > 5) yield break;
        if (node.ValueKind == JsonValueKind.Object)
        {
            bool match = hosts ? Field(node, "ip", "ip-address", "ipv4").Length > 0 && Field(node, "name", "hostname", "mac", "mac-address").Length > 0
                : Field(node, Src).Length > 0 && Field(node, Dst).Length > 0;
            if (match) { yield return node; yield break; }
            foreach (var p in node.EnumerateObject())
                foreach (var r in Records(p.Value, hosts, depth + 1)) yield return r;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var p in node.EnumerateArray())
                foreach (var r in Records(p, hosts, depth + 1)) yield return r;
    }

    private static bool EmptyNat(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array) return node.GetArrayLength() == 0;
        if (node.ValueKind != JsonValueKind.Object) return false;
        // Do not accept arbitrary empty objects or error envelopes as a successful NAT response.
        var props = node.EnumerateObject().ToArray();
        return props.Length == 1 && props[0].Name is "connections" or "nat" or "items"
            && props[0].Value.ValueKind == JsonValueKind.Array && props[0].Value.GetArrayLength() == 0;
    }

    public static NatResult Parse(string natJson, string hostsJson, string router, string timestamp, int sample, (uint Network, uint Mask) target)
    {
        var hosts = new Dictionary<string, (string Name, string Mac)>(StringComparer.Ordinal);
        bool missing = string.IsNullOrWhiteSpace(hostsJson);
        if (!missing)
        {
            try
            {
                using var clients = JsonDocument.Parse(hostsJson);
                foreach (var h in Records(clients.RootElement, true))
                    if (IPv4(Field(h, "ip", "ip-address", "address", "ipv4"), out var ip))
                        hosts.TryAdd(ip.ToString(), (Field(h, "name", "hostname", "host", "title"), Field(h, "mac", "mac-address", "hwaddr")));
            }
            catch (JsonException) { missing = true; }
        }
        using var nat = JsonDocument.Parse(natJson);
        var candidates = Records(nat.RootElement, false).ToArray();
        if (candidates.Length == 0 && !EmptyNat(nat.RootElement))
            throw new InvalidDataException(T("Unknown NAT response format. No successful empty report will be generated.", "Неизвестный формат NAT. Пустой отчёт как успешный не формируется."));
        var rows = new List<NatRow>();
        int skipped = 0;
        foreach (var n in candidates)
        {
            string src = Field(n, Src), dst = Field(n, Dst);
            if (!IPv4(src, out _) || !IPv4(dst, out _)) { skipped++; continue; }
            hosts.TryGetValue(src, out var host);
            string snat = Field(n, "src-out"), dnat = Field(n, "dst-out");
            bool s = Matches(src, target), d = Matches(dst, target);
            bool st = Matches(snat, target), dt = Matches(dnat, target);
            string direction = s && d ? "OFFICE_INTERNAL" : d ? "TO_OFFICE" : s ? "FROM_OFFICE" : st || dt ? "NAT_REFERENCE_REVIEW" : "NONE";
            var fields = new List<string>();
            if (d) fields.Add("DestinationIP"); if (s) fields.Add("SourceIP"); if (st) fields.Add("TranslatedSource"); if (dt) fields.Add("TranslatedDestination");
            rows.Add(new NatRow(timestamp, sample, router, host.Name ?? "", src, host.Mac ?? "",
                Field(n, "sport", "source-port", "source_port", "src-port"), dst, Field(n, "dport", "destination-port", "destination_port", "dst-port"),
                Field(n, "protocol", "proto", "type").ToUpperInvariant(), Field(n, "packets"), Field(n, "bytes"),
                snat, dnat, Field(n, "sport-out"), Field(n, "dport-out"), d ? "YES" : "NO", direction, string.Join(";", fields)));
        }
        if (candidates.Length > 0 && rows.Count == 0)
            throw new InvalidDataException(T("NAT entries exist, but IPv4 fields were not recognized.", "В NAT есть записи, но поля IPv4 не распознаны."));
        return new NatResult(rows, skipped, missing);
    }

    public static async Task<NatReport> CollectAsync(string address, bool https, string username, string password,
        string subnet, int samples, int interval, string resultsRoot, IProgress<string>? progress, CancellationToken ct)
    {
        var uri = RouterUri(address, https);
        var target = ParseTarget(subnet);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) throw new ArgumentException(T("Enter username and password.", "Введите логин и пароль."));
        if (samples is < 1 or > 12 || interval is < 3 or > 60) throw new ArgumentOutOfRangeException(nameof(samples));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer(), UseProxy = false };
        using var client = new HttpClient(handler) { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = MaxResponse };
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromMinutes(15));
        ct = limit.Token;
        await Authenticate(client, username, password, ct, progress);
        string hosts = "";
        try { hosts = await Get(client, "/rci/show/ip/hotspot/host", ct, progress); }
        catch (HttpRequestException) { progress?.Report(T("Client names unavailable; continuing NAT.", "Имена клиентов недоступны; продолжаю NAT.")); }
        var all = new List<NatRow>();
        int skipped = 0; bool hostMissing = false;
        for (int i = 1; i <= samples; i++)
        {
            if (i > 1) await Task.Delay(TimeSpan.FromSeconds(interval), ct);
            var json = await Get(client, "/rci/show/ip/nat", ct, progress);
            var parsed = Parse(json, hosts, address, DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture), i, target);
            all.AddRange(parsed.Rows); skipped += parsed.Skipped; hostMissing |= parsed.HostnamesUnavailable;
            progress?.Report(T("Sample ", "Снимок ") + i + "/" + samples + ": " + parsed.Rows.Count);
        }
        ct.ThrowIfCancellationRequested();
        string folder = Path.Combine(resultsRoot, "NAT_" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        string allPath = Path.Combine(folder, "01_All_Connections.csv");
        string targetPath = Path.Combine(folder, "02_Target_Connections.csv");
        WriteCsv(allPath, all, all);
        var matching = all.Where(r => r.TargetMatch == "YES").ToArray();
        WriteCsv(targetPath, matching, all);
        WriteCsv(Path.Combine(folder, "05_Office_Related.csv"), all.Where(r => r.OfficeFields.Length != 0), all);
        return new NatReport(allPath, targetPath, all.Count, matching.Length, all.Count(r => r.OfficeFields.Length != 0), skipped, hostMissing);
    }

    private static string Cell(string s)
    {
        // Prevent spreadsheet formula interpretation of external router-provided strings.
        if (s.Length > 0 && ("=+-@\t\r".Contains(s[0]) || s.TrimStart().StartsWith("="))) s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static void WriteCsv(string path, IEnumerable<NatRow> rows, List<NatRow> all)
    {
        var counts = all.GroupBy(r => (r.SourceIP, r.SourcePort, r.DestinationIP, r.DestinationPort, r.Protocol))
            .ToDictionary(g => g.Key, g => g.Count());
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Timestamp,Sample,Router,SourceName,SourceIP,SourceMAC,SourcePort,DestinationIP,DestinationPort,Protocol,Packets,Bytes,TranslatedSource,TranslatedDestination,TranslatedSourcePort,TranslatedDestinationPort,TargetMatch,Repeat,ObservationCount,OfficeDirection,OfficeFields,EvidenceSource");
        foreach (var r in rows)
        {
            int count = counts[(r.SourceIP, r.SourcePort, r.DestinationIP, r.DestinationPort, r.Protocol)];
            writer.WriteLine(string.Join(",", new[] { r.Timestamp, r.Sample.ToString(CultureInfo.InvariantCulture), r.Router, r.SourceName, r.SourceIP, r.SourceMAC,
                r.SourcePort, r.DestinationIP, r.DestinationPort, r.Protocol, r.Packets, r.Bytes, r.TranslatedSource, r.TranslatedDestination, r.TranslatedSourcePort,
                r.TranslatedDestinationPort, r.TargetMatch, count > 1 ? "YES" : "NO", count.ToString(CultureInfo.InvariantCulture), r.OfficeDirection, r.OfficeFields, "KEENETIC_NAT" }.Select(Cell)));
        }
    }
}
