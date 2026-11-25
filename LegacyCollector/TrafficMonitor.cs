// Keenetic Observer 1.9.1: read-only per-device Traffic Monitor snapshots.
// The router's RRD history measures registered-device Internet usage, not all local/VPN flows.
// API actions: GET /auth, POST /auth (login only), GET /rci/show/ip/hotspot/summary and /host.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace KeeneticObserver
{
    internal sealed class TrafficRow
    {
        public string Rank = "", Name = "", IP = "", MAC = "", Download = "", Upload = "", Total = "", Status = "", Window = "";
        public long TotalBytes;
        public long? DownloadBytes, UploadBytes;
    }
    internal sealed class TrafficResult
    {
        public List<TrafficRow> Rows = new List<TrafficRow>();
        public string Folder = "", Csv = "", Excel = "", Window = "";
        public int ExcludedGroups;
    }
    internal static class TrafficMonitor
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 50000000, RecursionLimit = 100 };
        private static readonly string[] WindowNames = { "3 минуты", "1 час", "3 часа", "24 часа" };
        internal static string WindowName(int detail) { return WindowNames[Math.Max(0, Math.Min(3, detail))]; }

        internal static TrafficResult Run(string routerIp, string username, string password, bool https,
            bool remoteVpnMode, int detail, CancellationToken token, Action<string> log)
        {
            if (detail < 0 || detail > 3) throw new ArgumentOutOfRangeException("detail");
            Uri initialUri = Engine.ParseRouterUri(routerIp, https);
            if (remoteVpnMode) RemoteVpn.RequirePrivateTarget(initialUri);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeeneticObserver", "Results");
            string folder = Path.Combine(root, "TRAFFIC_Router_" + routerIp.Replace('.', '_').Replace(':', '_') + "_" + stamp);
            if (Directory.Exists(folder)) folder += "_" + Guid.NewGuid().ToString("N").Substring(0, 5);
            Directory.CreateDirectory(folder);
            string rawFolder = Path.Combine(folder, "Raw_JSON");
            Directory.CreateDirectory(rawFolder);
            Action<string> report = delegate(string message)
            {
                File.AppendAllText(Path.Combine(folder, "DIAGNOSTICS.log"),
                    "[" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine,
                    new UTF8Encoding(false));
                log(message);
            };
            report("TRAFFIC MONITOR // read-only, router=" + routerIp + ", window=" + WindowName(detail) + ", no configuration changes");
            report("TRAFFIC LIMIT // registered-device Internet history; not proof of all LAN/VPN traffic");
            try
            {
                Uri effectiveUri = Engine.UpgradeHttps(initialUri, token, report);
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.AllowAutoRedirect = false;
                    handler.UseCookies = true;
                    handler.CookieContainer = new CookieContainer();
                    handler.UseProxy = false;
                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(20);
                        client.MaxResponseContentBufferSize = 50_000_000;
                        client.BaseAddress = effectiveUri;
                        Engine.Authenticate(client, username, password, token, report);
                        token.ThrowIfCancellationRequested();
                        // One bounded summary request; a separate host lookup enriches the IP/MAC/name fields.
                        string endpoint = "/rci/show/ip/hotspot/summary?attribute=sumbytes&detail=" + detail + "&count=200";
                        string summary = Engine.Get(client, endpoint, token, report);
                        File.WriteAllText(Path.Combine(rawFolder, "traffic_summary.json"), summary, new UTF8Encoding(false));
                        token.ThrowIfCancellationRequested();
                        string hosts = "";
                        try
                        {
                            hosts = Engine.Get(client, "/rci/show/ip/hotspot/host", token, report);
                            File.WriteAllText(Path.Combine(rawFolder, "hosts.json"), hosts, new UTF8Encoding(false));
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception e) { report("HOST INFO // optional client lookup failed: " + e.GetType().Name); }
                        TrafficResult data = Parse(summary, hosts, detail);
                        data.Folder = folder;
                        data.Csv = Path.Combine(folder, "08_Top_Traffic.csv");
                        SaveCsv(data.Csv, data.Rows);
                        // Local exports only: neutral dark Excel + history comparison + informational flags.
                        try { TrafficAnalyticsExport.Export(data, routerIp, DateTimeOffset.Now, report); }
                        catch (Exception exportError)
                        {
                            report("NETWORK ANALYTICS ERROR // CSV is available; Excel/comparison export failed: " + exportError.GetType().Name + ": " + exportError.Message);
                        }
                        string notes = "KEENETIC OBSERVER v1.9.1 // TOP TRAFFIC" + Environment.NewLine +
                            "Router: " + routerIp + Environment.NewLine +
                            "Window: " + data.Window + Environment.NewLine +
                            "Read time: " + DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine +
                            "Observed registered hosts (top 200 before filtering): " + data.Rows.Count + Environment.NewLine +
                            "Skipped aggregate categories: " + data.ExcludedGroups + Environment.NewLine +
                            "SOURCE: GET /rci/show/ip/hotspot/summary?attribute=sumbytes&detail=" + detail + "&count=200" + Environment.NewLine +
                            "This is Keenetic's Traffic Monitor history, normally registered-device Internet traffic. Local LAN traffic and VPN-internal flows can be absent." + Environment.NewLine +
                            "The selected period is a router RRD window, NOT a live speed measurement or a billing-grade meter." + Environment.NewLine +
                            "CSV 08 = raw device ranking, CSV 09 = comparison with previous matching window, CSV 10 = diagnostic flags." + Environment.NewLine +
                            "Keenetic_Traffic_Network_Analytics.xlsx = styled report, same data. RRD sliding-window delta IS NOT transfer since previous run." + Environment.NewLine +
                            "Raw_JSON contains source evidence. No router configuration changes were made." + Environment.NewLine;
                        File.WriteAllText(Path.Combine(folder, "TRAFFIC_README.txt"), notes, Encoding.UTF8);
                        report("TOP TRAFFIC // parsed devices=" + data.Rows.Count + "; aggregate categories excluded=" + data.ExcludedGroups + "; CSV=" + data.Csv);
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                report("TRAFFIC ERROR // " + e.GetType().Name + ": " + e.Message);
                report("RAW EVIDENCE // " + folder);
                throw;
            }
        }

        private static Dictionary<string, object> AsDict(object value) { return value as Dictionary<string, object>; }
        private static object Find(Dictionary<string, object> item, params string[] keys)
        {
            if (item == null) return null;
            foreach (string key in keys)
            {
                object value;
                if (item.TryGetValue(key, out value)) return value;
                foreach (KeyValuePair<string, object> kv in item)
                    if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            }
            return null;
        }
        private static string Str(object value)
        {
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
        }
        private static long? Number(object value)
        {
            if (value == null || value is bool || value is IDictionary || value is IEnumerable && !(value is string)) return null;
            long result;
            if (long.TryParse(Str(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= 0) return result;
            return null;
        }
        private static bool IsAggregate(string name, string mac)
        {
            string[] aggregate = { "others", "other", "unregistered", "multicast", "other devices", "прочие устройства", "незарегистрированные" };
            return aggregate.Contains((name ?? "").Trim().ToLowerInvariant()) ||
                aggregate.Contains((mac ?? "").Trim().ToLowerInvariant());
        }
        private static void GatherCandidates(object node, List<Dictionary<string, object>> output, int depth)
        {
            if (node == null || depth > 12) return;
            Dictionary<string, object> d = AsDict(node);
            if (d != null)
            {
                // Accept only host-like structures with traffic counters, not metadata such as 't'.
                object total = Find(d, "sumbytes"), rx = Find(d, "rxbytes"), tx = Find(d, "txbytes");
                if (Number(total).HasValue || Number(rx).HasValue || Number(tx).HasValue)
                {
                    Dictionary<string, object> nested = AsDict(Find(d, "host", "device"));
                    string identity = Str(Find(d, "mac", "name", "hostname", "ip"));
                    if (identity.Length > 0 || nested != null) { output.Add(d); return; }
                }
                foreach (KeyValuePair<string, object> kv in d) GatherCandidates(kv.Value, output, depth + 1);
            }
            else
            {
                IEnumerable elements = node as IEnumerable;
                if (elements != null && !(node is string))
                    foreach (object el in elements) GatherCandidates(el, output, depth + 1);
            }
        }
        private static Dictionary<string, Dictionary<string, string>> HostsByMac(string hostsJson)
        {
            Dictionary<string, Dictionary<string, string>> known = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(hostsJson)) return known;
            object data;
            try { data = Json.DeserializeObject(hostsJson); }
            catch (ArgumentException) { return known; }
            List<Dictionary<string, object>> candidates = new List<Dictionary<string, object>>();
            FindHostObjects(data, candidates, 0);
            foreach (Dictionary<string, object> d in candidates)
            {
                Dictionary<string, object> nested = AsDict(Find(d, "host"));
                string mac = Str(Find(d, "mac"));
                if (mac.Length == 0) mac = Str(Find(nested, "mac"));
                if (mac.Length == 0) continue;
                string ip = Str(Find(d, "ip", "address"));
                if (ip.Length == 0) ip = Str(Find(nested, "ip", "address"));
                string name = Str(Find(d, "name", "hostname"));
                if (name.Length == 0) name = Str(Find(nested, "name", "hostname"));
                Dictionary<string, string> record;
                if (!known.TryGetValue(mac, out record))
                {
                    record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    record["ip"] = ip; record["name"] = name;
                    known[mac] = record;
                }
                else
                {
                    if (record["ip"].Length == 0) record["ip"] = ip;
                    if (record["name"].Length == 0) record["name"] = name;
                }
            }
            return known;
        }
        private static void FindHostObjects(object node, List<Dictionary<string, object>> result, int depth)
        {
            if (node == null || depth > 12) return;
            Dictionary<string, object> d = AsDict(node);
            if (d != null)
            {
                if (Str(Find(d, "mac")).Length > 0) { result.Add(d); return; }
                foreach (KeyValuePair<string, object> kv in d) FindHostObjects(kv.Value, result, depth + 1);
            }
            else
            {
                IEnumerable list = node as IEnumerable;
                if (list != null && !(node is string)) foreach (object item in list) FindHostObjects(item, result, depth + 1);
            }
        }
        private static string Bytes(long? bytes)
        {
            if (!bytes.HasValue) return "неизвестно";
            double n = bytes.Value;
            string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
            int i = 0;
            while (n >= 1024 && i < units.Length - 1) { n /= 1024d; i++; }
            return n.ToString(i == 0 ? "0" : "0.00", CultureInfo.InvariantCulture) + " " + units[i];
        }

        internal static TrafficResult Parse(string summaryJson, string hostsJson, int detail)
        {
            object root;
            try { root = Json.DeserializeObject(summaryJson); }
            catch (ArgumentException ex) { throw new InvalidDataException("Traffic summary is not JSON.", ex); }
            List<Dictionary<string, object>> candidates = new List<Dictionary<string, object>>();
            GatherCandidates(root, candidates, 0);
            TrafficResult result = new TrafficResult { Window = WindowName(detail) };
            Dictionary<string, Dictionary<string, string>> hosts = HostsByMac(hostsJson);
            foreach (Dictionary<string, object> item in candidates)
            {
                Dictionary<string, object> nested = AsDict(Find(item, "host", "device"));
                string mac = Str(Find(item, "mac"));
                if (mac.Length == 0) mac = Str(Find(nested, "mac"));
                string name = Str(Find(item, "name", "hostname"));
                if (name.Length == 0) name = Str(Find(nested, "name", "hostname"));
                string ip = Str(Find(item, "ip", "address"));
                if (ip.Length == 0) ip = Str(Find(nested, "ip", "address"));
                if (IsAggregate(name, mac)) { result.ExcludedGroups++; continue; }
                Dictionary<string, string> mapped;
                if (hosts.TryGetValue(mac, out mapped))
                {
                    if (name.Length == 0) name = mapped["name"];
                    if (ip.Length == 0) ip = mapped["ip"];
                }
                if (name.Length == 0) name = mac.Length > 0 ? mac : ip;
                if (name.Length == 0) continue;
                long? rx = Number(Find(item, "rxbytes")), tx = Number(Find(item, "txbytes"));
                long? total = Number(Find(item, "sumbytes"));
                if (!total.HasValue && rx.HasValue && tx.HasValue) total = rx.Value + tx.Value;
                if (!total.HasValue) total = rx ?? tx;
                if (!total.HasValue) continue;
                result.Rows.Add(new TrafficRow { Name = name, IP = ip.Length > 0 ? ip : "—", MAC = mac.Length > 0 ? mac : "—",
                    Download = Bytes(rx), Upload = Bytes(tx), Total = Bytes(total), TotalBytes = total.Value,
                    DownloadBytes = rx, UploadBytes = tx, Window = result.Window,
                    Status = new[] { "yes", "true", "1" }.Contains(Str(Find(item, "active")).ToLowerInvariant()) ? "RRD active" :
                        new[] { "no", "false", "0" }.Contains(Str(Find(item, "active")).ToLowerInvariant()) ? "RRD inactive" : "неизвестно" });
            }
            if (candidates.Count == 0)
            {
                // An empty real host list is possible; otherwise avoid a false 'zero traffic' claim.
                Dictionary<string, object> d = AsDict(root);
                object possibleHosts = Find(d, "host", "hosts");
                bool empty = possibleHosts is object[] a && a.Length == 0 || possibleHosts is ArrayList list && list.Count == 0;
                if (!empty) throw new InvalidDataException("Unsupported traffic-summary JSON shape. Raw evidence saved; no fake zero-traffic result.");
            }
            // A router can return same MAC more than once; choose maximum within this one summary response.
            result.Rows = result.Rows.GroupBy(x => x.MAC != "—" ? "mac:" + x.MAC.ToLowerInvariant() : "ip:" + x.IP + "|" + x.Name,
                    StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.TotalBytes).First())
                .OrderByDescending(x => x.TotalBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < result.Rows.Count; i++) result.Rows[i].Rank = (i + 1).ToString(CultureInfo.InvariantCulture);
            return result;
        }
        private static string Csv(string s)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        private static void SaveCsv(string path, IEnumerable<TrafficRow> rows)
        {
            using (StreamWriter stream = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                stream.WriteLine("Rank,Name,IP,MAC,Download,Upload,Total,TotalBytes,Status,Window");
                foreach (TrafficRow r in rows)
                    stream.WriteLine(string.Join(",", new[] { Csv(r.Rank), Csv(r.Name), Csv(r.IP), Csv(r.MAC), Csv(r.Download),
                        Csv(r.Upload), Csv(r.Total), Csv(r.TotalBytes.ToString(CultureInfo.InvariantCulture)), Csv(r.Status), Csv(r.Window) }));
            }
        }
        internal static void CheckSelfTestFixtures()
        {
            string sample = "{\"t\":255,\"host\":[" +
                "{\"host\":{\"mac\":\"aa:bb:cc:dd:ee:01\",\"name\":\"CAFE-PC\"},\"sumbytes\":4096,\"rxbytes\":3072,\"txbytes\":1024}," +
                "{\"mac\":\"aa:bb:cc:dd:ee:02\",\"name\":\"BIG-PC\",\"sumbytes\":8192}," +
                "{\"mac\":\"others\",\"name\":\"others\",\"sumbytes\":999999}]}";
            string clients = "[{\"mac\":\"aa:bb:cc:dd:ee:01\",\"ip\":\"192.0.2.101\"}," +
                "{\"host\":{\"mac\":\"aa:bb:cc:dd:ee:02\",\"ip\":\"192.0.2.102\"}}]";
            TrafficResult r = Parse(sample, clients, 0);
            if (r.Rows.Count != 2 || r.Rows[0].TotalBytes != 8192 || r.Rows[0].IP != "192.0.2.102" ||
                r.Rows[1].DownloadBytes != 3072 || r.ExcludedGroups != 1)
                throw new InvalidDataException("TOP TRAFFIC summary test failed. rows=" + r.Rows.Count +
                " best=" + (r.Rows.Count > 0 ? r.Rows[0].Name + "/" + r.Rows[0].TotalBytes + "/" + r.Rows[0].IP : "none") +
                " second=" + (r.Rows.Count > 1 ? r.Rows[1].Name + "/" + r.Rows[1].DownloadBytes : "none") +
                " excluded=" + r.ExcludedGroups);
            bool denied = false;
            try { Parse("{\"error\":\"not supported\"}", "", 1); }
            catch (InvalidDataException) { denied = true; }
            if (!denied) throw new InvalidDataException("TOP TRAFFIC must reject unexpected API shape.");
            if (Parse("{\"host\":[]}", "", 3).Rows.Count != 0) throw new InvalidDataException("Empty history fixture failed.");
        }
    }

}