// Keenetic Observer 1.9.1 — Windows Forms / .NET Framework 4.8
// Read-only router operations: GET /auth; POST /auth for session login;
// GET /rci/show/version, /rci/show/ip/hotspot/host, /rci/show/ip/nat,
// optional GET /rci/show/ip/route and /rci/show/interface for VPN context (not traffic capture).
// No configuration changes, scans, or background service.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace KeeneticObserver
{
    internal sealed class Entry
    {
        public string Timestamp = "", Sample = "", Router = "", SourceName = "", SourceIP = "", SourceMAC = "";
        public string SourcePort = "", DestinationIP = "", DestinationPort = "", Protocol = "";
        public string Packets = "", Bytes = "", TranslatedSource = "", TranslatedDestination = "";
        public string TranslatedSourcePort = "", TranslatedDestinationPort = "", TargetMatch = "", Repeat = "NO", Note = "";
        public string ObservationCount = "1", FirstSeen = "", LastSeen = "";
        // Direction is based only on ORIGINAL src/dst; translated fields are marked REVIEW, not a proven connection direction.
        public string OfficeDirection = "", OfficeFields = "", EvidenceSource = "KEENETIC_NAT";
    }
    internal sealed class Host { public string IP = "", Name = "", MAC = ""; }
    internal sealed class Route
    {
        public string SourceName = "", SourceIP = "", DestinationIP = "", Protocol = "", DestinationPort = "";
        public string Observations = "", FirstSeen = "", LastSeen = "", Note = "";
        public string OfficeDirection = "", OfficeFields = "";
    }
    internal sealed class Snapshot { public int Number; public string Timestamp = "", Json = ""; }
    internal sealed class CollectionResult
    {
        public List<Entry> All = new List<Entry>();
        public List<Entry> Target = new List<Entry>();
        public List<Entry> OfficeRelated = new List<Entry>();
        public List<Route> Routes = new List<Route>();
        public List<Route> OfficeRelatedRoutes = new List<Route>();
        public List<Host> Hosts = new List<Host>();
        public string Folder = "", Excel = "";
        public bool Demo, Partial, HostnamesMissing, VpnRemote, Imported;
        public int SkippedRecords;
        public string ImportedFile = "";
        public string Status = "CONNECTED";
        public int RawOfficeReferences;
        public string RouteCheck = "NOT CHECKED", InterfaceCheck = "NOT CHECKED";
    }

    internal sealed class CollectorException : Exception
    {
        public readonly string Status;
        public CollectorException(string status, string message) : base(message) { Status = status; }
        public CollectorException(string status, string message, Exception inner) : base(message, inner) { Status = status; }
    }

    // VPN REMOTE only uses a VPN route that already exists in Windows or the site's gateway.
    // It does not install, enable, configure or disable VPNs or open router management to the Internet.
    internal sealed class RemoteProbeResult
    {
        public bool Reachable;
        public bool Timeout;
        public long ElapsedMilliseconds;
        public string LocalAddress = "", NetworkAdapter = "", Error = "";
    }
    internal static class RemoteVpn
    {
        internal static bool IsPrivateTarget(Uri router)
        {
            IPAddress ip;
            if (router == null || !IPAddress.TryParse(router.Host, out ip) ||
                ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }
        internal static void RequirePrivateTarget(Uri router)
        {
            if (!IsPrivateTarget(router))
                throw new InvalidOperationException("VPN REMOTE разрешает только внутренние IPv4-адреса (10/8, 172.16/12, 192.168/16 или 100.64/10). Публичную админку открывать не нужно.");
        }
        private static string AdapterForLocalAddress(string address)
        {
            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface nic in
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
                        if (item.Address.ToString() == address) return nic.Name;
            }
            catch (System.Net.NetworkInformation.NetworkInformationException) { }
            catch (System.Security.SecurityException) { }
            return "не определён";
        }
        // A TCP probe proves reachability of a private endpoint, NOT which physical/VPN path was used.
        internal static RemoteProbeResult ProbeTcp(Uri router, int timeoutMilliseconds)
        {
            RequirePrivateTarget(router);
            RemoteProbeResult result = new RemoteProbeResult();
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using (System.Net.Sockets.TcpClient socket = new System.Net.Sockets.TcpClient(System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    IAsyncResult pending = socket.BeginConnect(router.Host, router.Port, null, null);
                    try
                    {
                        if (!pending.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
                        {
                            result.Timeout = true;
                            result.Error = "TCP TIMEOUT: ответ от порта не получен. Проверьте VPN, маршруты и разрешения.";
                        }
                        else
                        {
                            socket.EndConnect(pending);
                            result.Reachable = true;
                            System.Net.IPEndPoint local = socket.Client.LocalEndPoint as System.Net.IPEndPoint;
                            result.LocalAddress = local == null ? "?" : local.Address.ToString();
                            result.NetworkAdapter = AdapterForLocalAddress(result.LocalAddress);
                        }
                    }
                    finally { pending.AsyncWaitHandle.Close(); }
                }
            }
            catch (System.Net.Sockets.SocketException e) { result.Error = "TCP " + e.SocketErrorCode; }
            catch (Exception e) { result.Error = e.GetType().Name; }
            result.ElapsedMilliseconds = watch.ElapsedMilliseconds;
            return result;
        }
        internal static void WriteProbeDiagnostic(Uri router, RemoteProbeResult result)
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeeneticObserver");
                Directory.CreateDirectory(root);
                File.AppendAllText(Path.Combine(root, "VPN_PROBE.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                    " private-endpoint=" + router.Host + ":" + router.Port +
                    " tcp=" + (result.Reachable ? "REACHABLE" : result.Timeout ? "TIMEOUT" : "FAILED") +
                    " elapsed-ms=" + result.ElapsedMilliseconds + " local-source=" + result.LocalAddress +
                    " adapter=" + result.NetworkAdapter + " detail=" + result.Error + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch (IOException) { /* Diagnostic logging must not prevent a manual connection. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static class Engine
    {
        public const string DemoSubnet = "203.0.113.0/24";
        public static string TargetSubnet { get; private set; } = DemoSubnet;
        private static byte[] targetNetwork = new byte[] {203,0,113,0};
        public static void SetTargetSubnet(string input)
        {
            string[] parts = (input ?? "").Trim().Split('/');
            if (parts.Length != 2 || parts[1] != "24" || !IPAddress.TryParse(parts[0], out IPAddress address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || address.ToString() != parts[0] || address.GetAddressBytes()[3] != 0)
                throw new ArgumentException("Enter a canonical IPv4 /24 network address, e.g. 203.0.113.0/24.");
            targetNetwork = address.GetAddressBytes();
            TargetSubnet = address + "/24";
            RawOfficeToken = MakeOfficeRegex(targetNetwork);
        }
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 50000000, RecursionLimit = 100 };
        private static readonly string[] AllCols = { "Timestamp", "Sample", "Router", "SourceName", "SourceIP", "SourceMAC", "SourcePort", "DestinationIP", "DestinationPort", "Protocol", "Packets", "Bytes", "TranslatedSource", "TranslatedDestination", "TranslatedSourcePort", "TranslatedDestinationPort", "TargetMatch", "Repeat", "ObservationCount", "FirstSeen", "LastSeen", "Note", "OfficeDirection", "OfficeFields", "EvidenceSource" };
        private static readonly string[] RouteCols = { "SourceName", "SourceIP", "DestinationIP", "Protocol", "DestinationPort", "Observations", "FirstSeen", "LastSeen", "Note" };
        private static readonly string[] OfficeRouteCols = { "SourceName", "SourceIP", "DestinationIP", "Protocol", "DestinationPort", "OfficeDirection", "OfficeFields", "Observations", "FirstSeen", "LastSeen", "Note" };
        private static readonly string[] HostCols = { "IP", "Name", "MAC" };
        // Raw JSON token evidence is NOT proof of a connection: it can appear in any metadata field.
        private static Regex RawOfficeToken = MakeOfficeRegex(targetNetwork);
        private static Regex MakeOfficeRegex(byte[] network) => new Regex(@"(?<![0-9.])" + Regex.Escape(network[0]+"."+network[1]+"."+network[2]+".") + @"(?:25[0-5]|2[0-4]\d|1?\d?\d)(?![0-9.])", RegexOptions.Compiled);
        private static readonly Regex V4 = new Regex(@"^\d{1,3}(\.\d{1,3}){3}$", RegexOptions.Compiled);

        public static bool TargetIP(string value)
        {
            // IPv4 /24, compare octets, never treat a different /24 as a match.
            IPAddress address;
            if (!IPAddress.TryParse(value, out address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            byte[] b = address.GetAddressBytes();
            return b[0] == targetNetwork[0] && b[1] == targetNetwork[1] && b[2] == targetNetwork[2];
        }


        internal static string OfficeEvidenceFields(Entry e)
        {
            List<string> matches = new List<string>();
            if (TargetIP(e.DestinationIP)) matches.Add("DestinationIP");
            if (TargetIP(e.SourceIP)) matches.Add("SourceIP");
            if (TargetIP(e.TranslatedSource)) matches.Add("TranslatedSource");
            if (TargetIP(e.TranslatedDestination)) matches.Add("TranslatedDestination");
            return string.Join(";", matches.ToArray());
        }

        internal static string OfficeDirectionFor(Entry e)
        {
            // Only original endpoint fields support directional labels.
            bool dst = TargetIP(e.DestinationIP), src = TargetIP(e.SourceIP);
            if (dst && src) return "OFFICE_INTERNAL";
            if (dst) return "TO_OFFICE";
            if (src) return "FROM_OFFICE";
            if (TargetIP(e.TranslatedSource) || TargetIP(e.TranslatedDestination)) return "NAT_REFERENCE_REVIEW";
            return "NONE";
        }

        internal static int RawOfficeTokenCount(string json)
        {
            return RawOfficeToken.Matches(json ?? "").Count;
        }

        public static Uri ParseRouterUri(string input, bool https)
        {
            // Router IP accepts IPv4 or IPv4:port. DNS/URLs are deliberately excluded.
            string[] parts = (input ?? "").Trim().Split(':');
            IPAddress ip;
            int port = https ? 443 : 80;
            if (parts.Length < 1 || parts.Length > 2 || !V4.IsMatch(parts[0]) ||
                !IPAddress.TryParse(parts[0], out ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new InvalidOperationException("Введите IPv4 или IPv4:port, например 192.0.2.1:8443.");
            byte[] octets = ip.GetAddressBytes();
            if (octets[0] == 0 || octets[0] == 127 || octets[0] >= 224)
                throw new InvalidOperationException("Укажите адрес обычного IPv4-узла, не loopback, multicast или broadcast.");
            if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535))
                throw new InvalidOperationException("Порт должен быть числом от 1 до 65535.");
            UriBuilder builder = new UriBuilder(https ? "https" : "http", ip.ToString(), port, "/");
            return builder.Uri;
        }

        private static bool SameRouter(Uri original, Uri destination)
        {
            IPAddress a, b;
            return destination != null && IPAddress.TryParse(original.Host, out a) &&
                IPAddress.TryParse(destination.Host, out b) && a.Equals(b);
        }

        internal static Uri UpgradeHttps(Uri initial, CancellationToken token, Action<string> report)
        {
            if (initial.Scheme != Uri.UriSchemeHttp) return initial;
            using (HttpClientHandler handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            using (HttpClient probe = new HttpClient(handler))
            {
                probe.Timeout = TimeSpan.FromSeconds(12);
                using (HttpResponseMessage response = probe.GetAsync(new Uri(initial, "/auth"), token).GetAwaiter().GetResult())
                {
                    if ((int)response.StatusCode < 300 || (int)response.StatusCode >= 400) return initial;
                    Uri next = response.Headers.Location;
                    if (next != null && !next.IsAbsoluteUri) next = new Uri(initial, next);
                    if (next != null && next.Scheme == Uri.UriSchemeHttps && SameRouter(initial, next))
                    {
                        UriBuilder safe = new UriBuilder(next) { Path = "/", Query = "", Fragment = "" };
                        report("HTTPS UPGRADE  //  Роутер запросил HTTPS, адрес IP подтверждён. Порт=" + safe.Port);
                        return safe.Uri;
                    }
                    throw new CollectorException("UNSUPPORTED DEVICE", "Неожиданный HTTP redirect при /auth. Автоматический переход на чужой адрес запрещён.");
                }
            }
        }

        internal static void CheckSelfTestFixtures()
        {
            ValidateNatJson("[]");
            ValidateNatJson("{}");
            bool rejected = false;
            try { ValidateNatJson("{\"mystery\":true}"); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new InvalidDataException("Unknown NAT structure was incorrectly accepted.");
            if (ParseRouterUri("192.0.2.1:8443", true).Port != 8443)
                throw new InvalidDataException("Router IP:port parsing failed.");
            if (TargetIP("198.51.100.1") || !TargetIP("203.0.113.31"))
                throw new InvalidDataException("Target /24 check failed.");
            foreach (string bad in new[] { "127.0.0.1", "0.0.0.0", "224.0.0.1", "255.255.255.255" })
            {
                bool accepted = false;
                try { ParseRouterUri(bad, false); accepted = true; }
                catch (InvalidOperationException) { }
                if (accepted) throw new InvalidDataException("Reserved router address was accepted: " + bad);
            }
            if (ParseRouterUri("192.0.2.0", false).Host != "192.0.2.0")
                throw new InvalidDataException("Valid /31-compatible address was rejected.");
            bool nullAccepted = true;
            try { ValidateNatJson("null"); }
            catch (InvalidDataException) { nullAccepted = false; }
            if (nullAccepted) throw new InvalidDataException("Unverified NAT null was incorrectly treated as an empty table.");
            if (SafeExcelCell(new string('X', 40000)).Length > 32767)
                throw new InvalidDataException("Excel maximum cell length was not enforced.");
            var generic = new Dictionary<string, object> { { "model", "Generic ISP Router" } };
            var marked = new Dictionary<string, object> { { "manufacturer", "Keenetic" } };
            if (HasKeeneticVersionSignal(generic) || !HasKeeneticVersionSignal(marked))
                throw new InvalidDataException("Version identification incorrectly trusted generic model field.");
        }

        // Fake in-memory transport: no network requests, no credentials saved or logged.
        private sealed class FakeAuthHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode code;
            public FakeAuthHandler(HttpStatusCode status) { code = status; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                return Task.FromResult(new HttpResponseMessage(code) {
                    Content = new StringContent("<html>test</html>", Encoding.UTF8, "text/html")
                });
            }
        }

        internal static void CheckOfficeEvidenceFixtures()
        {
            string folder = Path.Combine(Path.GetTempPath(), "KO_OfficeV17_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string body = "[" +
                    "{\"protocol\":\"tcp\",\"src\":\"192.0.2.101\",\"sport\":\"50000\",\"dst\":\"203.0.113.34\",\"dport\":\"1433\"}," +
                    "{\"protocol\":\"tcp\",\"src\":\"203.0.113.88\",\"sport\":\"443\",\"dst\":\"192.0.2.101\",\"dport\":\"50001\"}," +
                    "{\"protocol\":\"tcp\",\"src\":\"192.0.2.88\",\"sport\":\"50002\",\"dst\":\"198.51.100.250\",\"dport\":\"8080\",\"src-out\":\"203.0.113.78\"}," +
                    "{\"protocol\":\"udp\",\"src\":\"192.0.2.88\",\"sport\":\"50003\",\"dst\":\"198.51.100.9\",\"dport\":\"53\"}]";
                CollectionResult r = new CollectionResult { Folder = folder, RouteCheck = "AVAILABLE", InterfaceCheck = "AVAILABLE" };
                var samples = new List<Snapshot> { new Snapshot { Json = body, Number = 1, Timestamp = "2026-11-24 12:00:00" } };
                ProcessSnapshots(r, samples, "[]", "192.0.2.1", false, delegate(string x) { });
                if (r.All.Count != 4 || r.Target.Count != 1 || r.OfficeRelated.Count != 3 ||
                    r.OfficeRelatedRoutes.Count != 3 || r.RawOfficeReferences != 3)
                    throw new InvalidDataException("Office fixture expected All=4 ToOffice=1 Related=3 Raw=3.");
                if (r.OfficeRelated[0].OfficeDirection != "TO_OFFICE" ||
                    r.OfficeRelated[1].OfficeDirection != "FROM_OFFICE" ||
                    r.OfficeRelated[2].OfficeDirection != "NAT_REFERENCE_REVIEW" ||
                    r.All[3].OfficeDirection != "NONE")
                    throw new InvalidDataException("Office direction/evidence was classified incorrectly.");
                if (!File.Exists(Path.Combine(folder, "05_Office_Related.csv")) ||
                    !File.Exists(Path.Combine(folder, "OFFICE_VISIBILITY.txt")) || !File.Exists(r.Excel))
                    throw new InvalidDataException("Office CSV, XLSX or visibility diagnostics missing.");
                if (RawOfficeTokenCount("{\"note\":\"198.51.100.1\"}") != 0 ||
                    RawOfficeTokenCount("{\"note\":\"203.0.113.255\"}") != 1)
                    throw new InvalidDataException("Raw office token must match exact /24.");
            }
            finally { try { Directory.Delete(folder, true); } catch (IOException) { } }
        }

        internal static void CheckRemoteVpnSelfTestFixtures()
        {
            foreach (string example in new[] { new IPAddress(new byte[] { 10, 1, 2, 3 }).ToString(), new IPAddress(new byte[] { 172, 20, 1, 2 }).ToString(), new IPAddress(new byte[] { 192, 168, 1, 2 }).ToString(), "100.64.3.2" })
                if (!RemoteVpn.IsPrivateTarget(ParseRouterUri(example, true)))
                    throw new InvalidDataException("Valid VPN/private IPv4 range rejected: " + example);
            foreach (string publicIp in new[] { "8.8.8.8", "1.1.1.1", "203.0.113.8" })
                if (RemoteVpn.IsPrivateTarget(ParseRouterUri(publicIp, true)))
                    throw new InvalidDataException("Public IPv4 address allowed for VPN remote mode: " + publicIp);
            Uri exampleWithPort = ParseRouterUri(new IPAddress(new byte[] { 10, 1, 2, 3 }).ToString() + ":8443", true);
            if (exampleWithPort.Port != 8443 || exampleWithPort.Scheme != "https")
                throw new InvalidDataException("VPN target IP:port lost the selected HTTPS port.");
            bool rejected = false;
            try { RemoteVpn.RequirePrivateTarget(ParseRouterUri("203.0.113.9", false)); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidDataException("Remote mode did not reject a public IP.");
        }
        internal static void CheckAuthSelfTestFixtures()
        {
            using (HttpClient client = new HttpClient(new FakeAuthHandler(HttpStatusCode.Forbidden)))
            {
                client.BaseAddress = new Uri("http://192.0.2.1/");
                bool denied = false;
                try { Authenticate(client, "test", "fake-password", CancellationToken.None, delegate(string x) { }); }
                catch (CollectorException e) { denied = e.Status == "AUTH FAILED"; }
                if (!denied) throw new InvalidDataException("GET /auth 403 must be AUTH FAILED.");
            }
            using (HttpClient client = new HttpClient(new FakeAuthHandler(HttpStatusCode.OK)))
            {
                client.BaseAddress = new Uri("http://192.0.2.1/");
                bool unverified = false;
                Authenticate(client, "test", "fake-password", CancellationToken.None,
                    delegate(string x) { if (x.StartsWith("SESSION UNVERIFIED")) unverified = true; });
                if (!unverified) throw new InvalidDataException("GET /auth 200 must remain unverified until RCI succeeds.");
            }
        }
        internal static void CheckPartialSelfTestFixtures()
        {
            string folder = Path.Combine(Path.GetTempPath(), "KeeneticObserver_PartialTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var sample = new Snapshot {
                    Number = 1, Timestamp = "2026-11-23 12:00:00",
                    Json = "[{\"src\":\"192.0.2.12\",\"sport\":\"50123\",\"dst\":\"203.0.113.31\",\"dport\":\"443\",\"protocol\":\"tcp\"}]"
                };
                var result = new CollectionResult { Folder = folder, Demo = true };
                RecoverPartialReport(result, new List<Snapshot> { sample }, "", "192.0.2.1", true,
                    "STOPPED", delegate(string x) { });
                if (!result.Partial || result.All.Count != 1 || result.Target.Count != 1 ||
                    !result.Status.StartsWith("PARTIAL REPORT") || !File.Exists(result.Excel) ||
                    !Path.GetFileName(result.Excel).StartsWith("Partial_Report_") ||
                    !File.Exists(Path.Combine(folder, "01_All_Connections.csv")))
                    throw new InvalidDataException("Partial recovery did not expose a complete result with XLSX and CSV.");
                using (ZipArchive zip = ZipFile.OpenRead(result.Excel))
                    if (zip.GetEntry("xl/workbook.xml") == null) throw new InvalidDataException("Partial XLSX is incomplete.");
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }

        public static CollectionResult Run(bool demo, string routerIp, string username, string password,
                                           bool https, int samples, int interval, CancellationToken token, Action<string> log,
                                           bool remoteVpnMode = false)
        {
            Uri initialUri = demo ? null : ParseRouterUri(routerIp, https);
            if (!demo && remoteVpnMode) RemoteVpn.RequirePrivateTarget(initialUri);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeeneticObserver", "Results");
            string folder = Path.Combine(root, (demo ? "DEMO_" : (remoteVpnMode ? "VPN_Router_" : "Router_") + routerIp.Replace('.', '_').Replace(':', '_') + "_") + stamp);
            if (Directory.Exists(folder)) folder += "_" + Guid.NewGuid().ToString("N").Substring(0, 5);
            Directory.CreateDirectory(folder);
            string raw = Path.Combine(folder, "Raw_JSON");
            Directory.CreateDirectory(raw);
            string diagnosticPath = Path.Combine(folder, "DIAGNOSTICS.log");
            object diagnosticLock = new object();
            Action<string> report = delegate(string message)
            {
                string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message;
                lock (diagnosticLock) File.AppendAllText(diagnosticPath, line + Environment.NewLine, new UTF8Encoding(false));
                log(message);
            };
            report("SESSION  //  " + (demo ? "DEMO" : remoteVpnMode ? "REMOTE VPN" : "LIVE") + "; target=" + TargetSubnet + "; collector=1.9.1-port; no configuration writes.");
            if (!demo && remoteVpnMode)
                report("VPN REMOTE  //  Используется уже существующий маршрут Windows/шлюза. TCP-доступность не доказывает, что трафик идёт через VPN. Публичное управление не включается.");
            report("DIAGNOSTICS  //  Журнал текущего запуска: " + diagnosticPath);
            CollectionResult result = new CollectionResult { Demo = demo, Folder = folder, VpnRemote = remoteVpnMode && !demo };
            List<Snapshot> snaps = new List<Snapshot>();
            string hostsJson = "";
            bool hostsUnavailable = false;
            bool completed = false;
            try
            {
                if (demo)
                {
                    routerIp = "192.0.2.1";
                    samples = 2;
                    report("DEMO: никакие запросы к роутеру не отправляются.");
                    hostsJson = "[{\"ip\":\"192.0.2.12\",\"name\":\"KITCHEN-PC\",\"mac\":\"00:11:22:33:44:55\"},{\"ip\":\"192.0.2.110\",\"name\":\"OFFICE-PC\",\"mac\":\"00:11:22:33:44:66\"}]";
                    File.WriteAllText(Path.Combine(raw, "clients.json"), hostsJson, Encoding.UTF8);
                    string demoJson = "[" +
                        "{\"protocol\":\"tcp\",\"src\":\"192.0.2.12\",\"sport\":\"51010\",\"dst\":\"203.0.113.31\",\"dport\":\"443\",\"packets\":\"15\"}," +
                        "{\"protocol\":\"tcp\",\"src\":\"192.0.2.110\",\"sport\":\"51011\",\"dst\":\"203.0.113.200\",\"dport\":\"80\"}," +
                        "{\"protocol\":\"udp\",\"src\":\"192.0.2.12\",\"sport\":\"65013\",\"dst\":\"192.0.2.1\",\"dport\":\"53\"}," +
                        "{\"protocol\":\"tcp\",\"src\":\"192.0.2.12\",\"sport\":\"51012\",\"dst\":\"198.51.100.31\",\"dport\":\"443\"}," +
                        "{\"protocol\":\"tcp\",\"src\":\"192.0.2.12\",\"sport\":\"51013\",\"dst\":\"198.51.100.79\",\"dport\":\"5656\"}" + "]";
                    for (int i = 1; i <= samples; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        File.WriteAllText(Path.Combine(raw, "nat_sample_" + i + ".json"), demoJson, Encoding.UTF8);
                        snaps.Add(new Snapshot { Number = i, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Json = demoJson });
                    }
                }
                else
                {
                    report("Соединение с " + routerIp + " по " + (https ? "HTTPS" : "HTTP") + " (только чтение)...");
                    Uri effectiveUri = UpgradeHttps(initialUri, token, report);
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
                            report("PREFLIGHT  //  Проверяю KeeneticOS API и схему авторизации...");
                            Authenticate(client, username, password, token, report);
                            report("AUTH CHECK  //  /auth обработан; право доступа подтвердится успешным RCI-запросом.");
                            string firmware = "";
                            try
                            {
                                firmware = Get(client, "/rci/show/version", token, report);
                                // Save the raw response before checking format. Generic model fields are NOT proof of Keenetic.
                                File.WriteAllText(Path.Combine(raw, "version.json"), firmware, Encoding.UTF8);
                                Dictionary<string, object> versionObject = Json.DeserializeObject(firmware) as Dictionary<string, object>;
                                if (versionObject == null)
                                    report("VERSION WARNING  //  Формат ответа версии не распознан; NAT API будет проверен отдельно.");
                                else if (HasKeeneticVersionSignal(versionObject))
                                    report("VERSION SIGNAL  //  Обнаружены признаки KeeneticOS; доступность NAT API ещё не подтверждена.");
                                else
                                    report("VERSION WARNING  //  Нет надёжных признаков KeeneticOS; проверю NAT API отдельно.");
                            }
                            catch (CollectorException e)
                            {
                                if (e.Status == "UNSUPPORTED DEVICE")
                                    report("VERSION INFO  //  Команда version недоступна; проверка NAT API продолжится.");
                                else throw;
                            }
                            catch (Exception e) { report("VERSION INFO  //  " + e.GetType().Name + "; проверяю NAT API отдельно."); }
                            try
                            {
                                hostsJson = Get(client, "/rci/show/ip/hotspot/host", token, report);
                                File.WriteAllText(Path.Combine(raw, "clients.json"), hostsJson, Encoding.UTF8);
                            }
                            catch (Exception e) { hostsUnavailable = true; report("Имена клиентов недоступны: " + e.Message); }
                            // Actual NAT preflight: validate one response before entering sampling loop.
                            report("PREFLIGHT NAT  //  Проверяю доступность и формат таблицы...");
                            string firstBody = Get(client, "/rci/show/ip/nat", token, report);
                            File.WriteAllText(Path.Combine(raw, "nat_sample_1.json"), firstBody, Encoding.UTF8);
                            try { ValidateNatJson(firstBody); }
                            catch (InvalidDataException e) { throw new CollectorException("UNSUPPORTED DEVICE", "Неподдерживаемая структура NAT JSON: " + e.Message, e); }
                            report("CONNECTED  //  Доступ и формат NAT API проверены. Это не гарантирует видимость всех Ethernet/LAN соединений.");
                            snaps.Add(new Snapshot { Number = 1, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Json = firstBody });
                            report("Снимок 1/" + samples + " получен.");
                            // RCI show-only diagnostics are optional and never treated as proof of a client->office flow.
                            result.RouteCheck = TryReadNetworkContext(client, "/rci/show/ip/route", "office_ip_routes.json", raw, token, report);
                            result.InterfaceCheck = TryReadNetworkContext(client, "/rci/show/interface", "office_interfaces.json", raw, token, report);
                            for (int i = 2; i <= samples; i++)
                            {
                                if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(interval))) token.ThrowIfCancellationRequested();
                                token.ThrowIfCancellationRequested();
                                string body = Get(client, "/rci/show/ip/nat", token, report);
                                File.WriteAllText(Path.Combine(raw, "nat_sample_" + i + ".json"), body, Encoding.UTF8);
                                try { ValidateNatJson(body); }
                                catch (InvalidDataException e) { throw new CollectorException("UNSUPPORTED DEVICE", "Неподдерживаемая структура NAT JSON: " + e.Message, e); }
                                snaps.Add(new Snapshot { Number = i, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Json = body });
                                report("Снимок " + i + "/" + samples + " получен.");
                            }
                        }
                    }
                }
                ProcessSnapshots(result, snaps, hostsJson, routerIp, hostsUnavailable, report);
                completed = true;
                report("Готово: " + result.All.Count + " NAT-наблюдений, " + result.Target.Count + " TO OFFICE, " +
                    result.OfficeRelated.Count + " RELATED, " + result.Routes.Count + " офисных направлений.");
                return result;
            }
            catch (Exception e)
            {
                string kind = Classify(e, token);
                bool recovered = false;
                if (snaps.Count > 0 && !completed)
                {
                    try
                    {
                        RecoverPartialReport(result, snaps, hostsJson, routerIp, hostsUnavailable, kind, report);
                        recovered = true;
                    }
                    catch (Exception salvageError)
                    {
                        try { report("PARTIAL REPORT FAILED  //  " + salvageError.GetType().Name + ": " + salvageError.Message); }
                        catch { /* Do not hide the original failure if disk/log writes also fail. */ }
                    }
                }
                try
                {
                    report(kind + "  //  " + Friendly(e, kind));
                    report("REPORT DIRECTORY  //  " + folder);
                    // Do not log password, auth headers, cookies or raw HTTP responses.
                    File.AppendAllText(diagnosticPath, "ExceptionType=" + e.GetType().FullName + Environment.NewLine +
                        "InnerType=" + (e.InnerException == null ? "none" : e.InnerException.GetType().FullName) + Environment.NewLine,
                        new UTF8Encoding(false));
                }
                catch { /* Preserve original exception. */ }
                // Successful recovery is a completed PARTIAL result, not an ordinary success.
                // Returning it allows the GUI's OPEN EXCEL and last-result table to find the saved report.
                if (recovered) return result;
                throw;
            }
        }

        private static void RecoverPartialReport(CollectionResult result, List<Snapshot> snaps, string hostsJson,
                                                 string routerIp, bool hostsUnavailable, string failureKind, Action<string> report)
        {
            result.Partial = true;
            result.Status = "PARTIAL REPORT (" + failureKind + ")";
            var safeSnaps = new List<Snapshot>();
            foreach (Snapshot snap in snaps)
            {
                try { ValidateNatJson(snap.Json); safeSnaps.Add(snap); }
                catch (Exception invalid) { report("SALVAGE SKIP  //  sample=" + snap.Number + "; " + invalid.GetType().Name); }
            }
            if (safeSnaps.Count == 0) throw new InvalidDataException("Нет пригодных для частичного отчёта снимков.");
            ProcessSnapshots(result, safeSnaps, hostsJson, routerIp, hostsUnavailable, report);
            if (string.IsNullOrEmpty(result.Excel) || !File.Exists(result.Excel))
                throw new IOException("Частичный XLSX не сформирован; проверьте сохранённые CSV и DIAGNOSTICS.log.");
            report("PARTIAL REPORT  //  Сохранены " + result.All.Count + " наблюдений. XLSX=" + result.Excel);
        }


        // Optional show-only checks. A route/interface response is context, NOT a connection table.
        // Short independent timeout to avoid delaying the actual NAT collection on older firmware.
        private static string TryReadNetworkContext(HttpClient client, string endpoint, string filename,
                                                    string rawFolder, CancellationToken token, Action<string> report)
        {
            using (CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                limited.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    string body = Get(client, endpoint, limited.Token, report);
                    object parsed = Json.DeserializeObject(body);
                    if (!(parsed is object[]) && !(parsed is Dictionary<string, object>))
                        throw new InvalidDataException("Context endpoint did not return a JSON object/array.");
                    File.WriteAllText(Path.Combine(rawFolder, filename), body, Encoding.UTF8);
                    report("OFFICE CONTEXT  //  " + endpoint + " JSON available; routes/interfaces are not individual flows.");
                    return "AVAILABLE (read-only, not flow evidence)";
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested) throw;
                    report("OFFICE CONTEXT  //  " + endpoint + " timed out; NAT collection continues.");
                    return "TIMEOUT (nonfatal)";
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();
                    report("OFFICE CONTEXT  //  " + endpoint + " unavailable (" + e.GetType().Name + "); NAT collection continues.");
                    return "UNAVAILABLE (nonfatal)";
                }
            }
        }

        private static void ProcessSnapshots(CollectionResult result, List<Snapshot> snaps, string hostsJson,
                                             string routerIp, bool hostsUnavailable, Action<string> report)
        {
            result.All.Clear(); result.Target.Clear(); result.OfficeRelated.Clear();
            result.Routes.Clear(); result.OfficeRelatedRoutes.Clear(); result.Hosts.Clear();
            result.SkippedRecords = 0;
            result.RawOfficeReferences = snaps.Sum(x => RawOfficeTokenCount(x.Json));
                if (hostsJson.Length != 0)
                {
                    try
                    {
                        foreach (Dictionary<string, object> item in FindRecords(Json.DeserializeObject(hostsJson), 0, true))
                        {
                            string ip = Field(item, "ip", "ip-address", "address", "ipv4");
                            if (V4.IsMatch(ip)) result.Hosts.Add(new Host { IP = ip, Name = Field(item, "name", "hostname", "host", "title"), MAC = Field(item, "mac", "mac-address", "hwaddr") });
                        }
                    }
                    catch (Exception ex) { result.HostnamesMissing = true; report("Не удалось распознать имена клиентов: " + ex.Message); }
                }
                Dictionary<string, Host> known = new Dictionary<string, Host>(StringComparer.OrdinalIgnoreCase);
                foreach (Host h in result.Hosts) if (!known.ContainsKey(h.IP)) known.Add(h.IP, h);
                foreach (Snapshot snap in snaps)
                {
                    object decoded = Json.DeserializeObject(snap.Json);
                    List<Dictionary<string, object>> rows = FindRecords(decoded, 0, false).ToList();
                    if (rows.Count == 0 && !IsEmptyNat(decoded))
                        throw new InvalidDataException("Неизвестный формат NAT JSON. Исходный ответ сохранён в Raw_JSON.");
                    int parsedCount = 0;
                    foreach (Dictionary<string, object> n in rows)
                    {
                        string src = Field(n, "src", "source", "source-ip", "source_ip", "src-ip");
                        string dst = Field(n, "dst", "destination", "destination-ip", "destination_ip", "dst-ip");
                        if (!V4.IsMatch(src) || !V4.IsMatch(dst)) { result.SkippedRecords++; continue; }
                        IPAddress si, di;
                        if (!IPAddress.TryParse(src, out si) || !IPAddress.TryParse(dst, out di)) { result.SkippedRecords++; continue; }
                        string proto = Field(n, "protocol", "proto", "type").ToUpperInvariant();
                        string dport = Field(n, "dport", "destination-port", "destination_port", "dst-port");
                        Host meta = known.ContainsKey(src) ? known[src] : null;
                        Entry e = new Entry {
                            Timestamp = snap.Timestamp, Sample = snap.Number.ToString(), Router = routerIp,
                            SourceIP = src, SourceName = meta == null ? "" : meta.Name, SourceMAC = meta == null ? "" : meta.MAC,
                            SourcePort = Field(n, "sport", "source-port", "source_port", "src-port"),
                            DestinationIP = dst, DestinationPort = dport, Protocol = proto,
                            Packets = Field(n, "packets"), Bytes = Field(n, "bytes"),
                            TranslatedSource = Field(n, "src-out"), TranslatedDestination = Field(n, "dst-out"),
                            TranslatedSourcePort = Field(n, "sport-out"), TranslatedDestinationPort = Field(n, "dport-out"),
                            TargetMatch = TargetIP(dst) ? "YES" : "NO", EvidenceSource = "KEENETIC_NAT",
                            Note = dst == routerIp.Split(':')[0] && dport == "53" && proto == "UDP" ? "LOCAL_ROUTER_DNS" : ""
                        };
                        e.OfficeFields = OfficeEvidenceFields(e);
                        e.OfficeDirection = OfficeDirectionFor(e);
                        result.All.Add(e);
                        parsedCount++;
                        if (e.TargetMatch == "YES") result.Target.Add(e);
                        if (e.OfficeFields.Length != 0) result.OfficeRelated.Add(e);
                    }
                    report("PARSE  //  sample=" + snap.Number + "; candidate_records=" + rows.Count + "; accepted=" + parsedCount + "; skipped=" + (rows.Count - parsedCount) +
                        "; office_outbound=" + result.Target.Count + "; office_related=" + result.OfficeRelated.Count +
                        "; raw_office_tokens=" + RawOfficeTokenCount(snap.Json));
                    if (rows.Count > 0 && parsedCount == 0)
                        throw new InvalidDataException("NAT содержит записи, но IP-поля не распознаны. Сырой ответ в Raw_JSON.");
                }
                Summarize(result);
                report("OFFICE COVERAGE  //  source=NAT; to-office=" + result.Target.Count +
                    "; related-any-field=" + result.OfficeRelated.Count + "; raw-JSON-mentions=" + result.RawOfficeReferences +
                    ". Zero does NOT prove zero VPN traffic.");
                result.HostnamesMissing |= hostsUnavailable;
                if (result.HostnamesMissing && !result.Partial) result.Status = "CONNECTED (PARTIAL: no hostnames)";
                SaveReport(result, CancellationToken.None);
        }

        internal static void Summarize(CollectionResult result)
        {
            // Keep duplicate snapshots in the full log, mark repeated five-tuples.
            foreach (var group in result.All.GroupBy(e => e.SourceIP + "|" + e.SourcePort + "|" + e.DestinationIP + "|" + e.DestinationPort + "|" + e.Protocol))
                foreach (Entry e in group) { e.ObservationCount = group.Count().ToString(); e.FirstSeen = group.First().Timestamp; e.LastSeen = group.Last().Timestamp; if (group.Count() > 1) e.Repeat = "YES"; }
            foreach (var group in result.Target.GroupBy(e => e.SourceIP + "|" + e.DestinationIP + "|" + e.Protocol + "|" + e.DestinationPort))
            {
                Entry first = group.First(), last = group.Last();
                result.Routes.Add(new Route { SourceName = first.SourceName, SourceIP = first.SourceIP,
                    DestinationIP = first.DestinationIP, DestinationPort = first.DestinationPort,
                    Protocol = first.Protocol, Observations = group.Count().ToString(), FirstSeen = first.Timestamp,
                    LastSeen = last.Timestamp, Note = first.Note });
            }
            foreach (var group in result.OfficeRelated.GroupBy(e => e.SourceIP + "|" + e.DestinationIP + "|" + e.Protocol + "|" + e.DestinationPort + "|" + e.OfficeDirection))
            {
                Entry first = group.First(), last = group.Last();
                result.OfficeRelatedRoutes.Add(new Route { SourceName = first.SourceName, SourceIP = first.SourceIP,
                    DestinationIP = first.DestinationIP, DestinationPort = first.DestinationPort,
                    Protocol = first.Protocol, OfficeDirection = first.OfficeDirection, OfficeFields = string.Join(";", group.SelectMany(x => x.OfficeFields.Split(';')).Where(x => x.Length > 0).Distinct().ToArray()),
                    Observations = group.Count().ToString(), FirstSeen = first.Timestamp,
                    LastSeen = last.Timestamp, Note = first.Note });
            }
            result.OfficeRelatedRoutes = result.OfficeRelatedRoutes.OrderBy(x => x.OfficeDirection).ThenBy(x => x.SourceIP).ToList();
            result.Routes = result.Routes.OrderBy(r => r.SourceIP).ThenBy(r => r.DestinationIP).ToList();
        }

        public static string Classify(Exception e, CancellationToken token)
        {
            if (e is OperationCanceledException && token.IsCancellationRequested) return "STOPPED";
            CollectorException known = e as CollectorException;
            if (known != null) return known.Status;
            if (e is UnauthorizedAccessException) return "AUTH FAILED";
            if (e is TimeoutException || e is OperationCanceledException) return token.IsCancellationRequested ? "STOPPED" : "TIMEOUT";
            Exception current = e;
            while (current != null)
            {
                var socket = current as System.Net.Sockets.SocketException;
                if (socket != null)
                {
                    if (socket.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut) return "TIMEOUT";
                    return "NETWORK ERROR";
                }
                current = current.InnerException;
            }
            if (e is HttpRequestException || e is WebException) return "NETWORK ERROR";
            return "ERROR";
        }
        public static string Friendly(Exception e, string kind)
        {
            if (kind == "TIMEOUT") return "Роутер не ответил вовремя. Проверьте IP, доступность сети, HTTP/HTTPS.";
            if (kind == "AUTH FAILED") return "Неверные учётные данные или недостаточно прав на RCI.";
            if (kind == "UNSUPPORTED DEVICE") return e.Message;
            if (kind == "STOPPED") return "Операция остановлена пользователем.";
            if (kind == "SERVER ERROR") return "Keenetic ответил ошибкой HTTP 5xx. Проверьте журнал и состояние роутера.";
            if (kind == "NETWORK ERROR") return "Ошибка сети/TLS или соединение закрыто. Проверьте адрес, схему HTTP(S), доступ к веб-интерфейсу.";
            return e.Message;
        }

        private static void ValidateNatJson(string body)
        {
            object decoded;
            try { decoded = Json.DeserializeObject(body); }
            catch (Exception e) { throw new InvalidDataException("NAT API вернул некорректный JSON: " + e.Message); }
            if (FindRecords(decoded, 0, false).Any() || IsEmptyNat(decoded)) return;
            throw new InvalidDataException("Неизвестный формат NAT JSON. Исходный ответ сохранён в Raw_JSON.");
        }
        private static bool IsEmptyNat(object decoded)
        {
            object[] arr = decoded as object[];
            if (arr != null) return arr.Length == 0;
            Dictionary<string, object> d = decoded as Dictionary<string, object>;
            if (d == null) return false;
            if (d.Count == 0) return true;
            foreach (string key in new[] { "connections", "nat", "items" })
            {
                object v;
                if (TryGet(d, key, out v)) { object[] a = v as object[]; if (a != null && a.Length == 0) return true; }
            }
            return false;
        }
        private static IEnumerable<Dictionary<string, object>> FindRecords(object data, int depth, bool hosts)
        {
            if (data == null || depth > 5) yield break;
            object[] arr = data as object[];
            if (arr != null) { foreach (object obj in arr) foreach (Dictionary<string, object> r in FindRecords(obj, depth + 1, hosts)) yield return r; yield break; }
            Dictionary<string, object> d = data as Dictionary<string, object>;
            if (d == null) yield break;
            if (hosts ? (Has(d, "ip", "ip-address", "ipv4") && Has(d, "name", "hostname", "mac", "mac-address")) :
                (Has(d, "src", "source", "source-ip", "source_ip", "src-ip") && Has(d, "dst", "destination", "destination-ip", "destination_ip", "dst-ip")))
            { yield return d; yield break; }
            foreach (KeyValuePair<string, object> pair in d)
                if (pair.Value is object[] || pair.Value is Dictionary<string, object>)
                    foreach (Dictionary<string, object> r in FindRecords(pair.Value, depth + 1, hosts)) yield return r;
        }
        private static bool Has(Dictionary<string, object> d, params string[] names) { foreach (string n in names) { object v; if (TryGet(d, n, out v)) return true; } return false; }
        private static bool HasKeeneticVersionSignal(Dictionary<string, object> d)
        {
            if (d == null) return false;
            if (Has(d, "hw_id", "ndw", "ndw4")) return true;
            string vendor = Field(d, "manufacturer", "vendor");
            return vendor.IndexOf("keenetic", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static string Field(Dictionary<string, object> d, params string[] names)
        {
            foreach (string n in names) { object val; if (TryGet(d, n, out val) && val != null) return Convert.ToString(val, CultureInfo.InvariantCulture); }
            return "";
        }
        private static bool TryGet(Dictionary<string, object> d, string name, out object value)
        {
            if (d.TryGetValue(name, out value)) return true;
            foreach (KeyValuePair<string, object> kv in d) if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) { value = kv.Value; return true; }
            value = null; return false;
        }

        internal static string Get(HttpClient client, string path, CancellationToken token, Action<string> report)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            HttpResponseMessage outgoing;
            try { outgoing = client.GetAsync(path, token).GetAwaiter().GetResult(); }
            catch { report("HTTP  //  GET " + path + " transport failure after " + timer.ElapsedMilliseconds + "ms"); throw; }
            using (HttpResponseMessage response = outgoing)
            {
                report("HTTP  //  GET " + path + " status=" + (int)response.StatusCode + "; ms=" + timer.ElapsedMilliseconds);
                // Avoid reading/logging entire bodies for error pages.
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new CollectorException("AUTH FAILED", "Нет прав для GET " + path + " (HTTP " + (int)response.StatusCode + ").");
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed)
                    throw new CollectorException("UNSUPPORTED DEVICE", "Команда " + path + " отсутствует или выключена (HTTP " + (int)response.StatusCode + ").");
                if ((int)response.StatusCode >= 500)
                    throw new CollectorException("SERVER ERROR", "GET " + path + " вернул HTTP " + (int)response.StatusCode);
                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                    throw new CollectorException("UNSUPPORTED DEVICE", "GET " + path + " перенаправлен HTTP " + (int)response.StatusCode + ". Проверьте HTTPS и порт.");
                if (!response.IsSuccessStatusCode) throw new CollectorException("HTTP ERROR", "GET " + path + " вернул HTTP " + (int)response.StatusCode);
                string data = response.Content.ReadAsStringAsync(token).GetAwaiter().GetResult();
                return data;
            }
        }
        internal static void Authenticate(HttpClient client, string user, string password, CancellationToken token, Action<string> report)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            using (HttpResponseMessage response = client.GetAsync("/auth", token).GetAwaiter().GetResult())
            {
                report("HTTP  //  GET /auth status=" + (int)response.StatusCode + "; ms=" + watch.ElapsedMilliseconds);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Existing session may be valid, but a 200 HTML captive portal is not proof.
                    report("SESSION UNVERIFIED  //  /auth вернул HTTP 200; требуется проверка RCI API.");
                    return;
                }
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    throw new CollectorException("AUTH FAILED", "Доступ к /auth запрещён (HTTP 403). Проверьте права и настройки доступа к роутеру.");
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed)
                    throw new CollectorException("UNSUPPORTED DEVICE", "Нет ожидаемого Keenetic /auth endpoint (HTTP " + (int)response.StatusCode + ").");
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                    throw new CollectorException((int)response.StatusCode >= 500 ? "SERVER ERROR" : "UNSUPPORTED DEVICE", "Неожиданный ответ /auth: HTTP " + (int)response.StatusCode + ".");
                string scheme = Header(response, "WWW-Authenticate");
                if (scheme.IndexOf("x-ndw4", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new CollectorException("UNSUPPORTED AUTH", "Эта KeeneticOS использует x-ndw4; безопасная авторизация для неё пока не реализована. Изменений на роутере не было.");
                string realm = Header(response, "X-NDM-Realm"), challenge = Header(response, "X-NDM-Challenge");
                if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(challenge))
                    throw new CollectorException("UNSUPPORTED AUTH", "Нет поддерживаемой x-ndw2 challenge-response авторизации. Возможно другая прошивка или устройство.");
                if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Введите пароль администратора Keenetic.");
                string baseString = user + ":" + realm + ":" + password;
                string md5;
                using (MD5 md = MD5.Create()) md5 = Hex(md.ComputeHash(Encoding.UTF8.GetBytes(baseString)));
                string sha;
                using (SHA256 sh = SHA256.Create()) sha = Hex(sh.ComputeHash(Encoding.UTF8.GetBytes(challenge + md5)));
                string payload = "{\"login\":" + Json.Serialize(user) + ",\"password\":" + Json.Serialize(sha) + "}";
                using (StringContent body = new StringContent(payload, Encoding.UTF8, "application/json"))
                using (HttpResponseMessage logged = client.PostAsync("/auth", body, token).GetAwaiter().GetResult())
                {
                    report("HTTP  //  POST /auth status=" + (int)logged.StatusCode + "; ms=" + watch.ElapsedMilliseconds);
                    if ((int)logged.StatusCode >= 500) throw new CollectorException("SERVER ERROR", "POST /auth вернул HTTP " + (int)logged.StatusCode);
                    if (logged.StatusCode != HttpStatusCode.OK) throw new UnauthorizedAccessException("HTTP " + (int)logged.StatusCode);
                }
            }
        }
        private static string Header(HttpResponseMessage response, string name)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues(name, out values)) return string.Join(",", values.ToArray());
            return "";
        }
        private static string Hex(byte[] value) { return BitConverter.ToString(value).Replace("-", "").ToLowerInvariant(); }

        internal static string SaveAccessReport(string folder,AccessAnalysis a)
        {
            Directory.CreateDirectory(folder);
            string[] endpointCols={"IP","Port","Protocol","Observations","Sources","Routers","FirstSeen","LastSeen","TcpStatus","CheckedAt","CheckDetail","ServiceHint","Review"};
            string[] sourceCols={"Router","SourceIP","SourceName","SourceMAC","DestinationIP","DestinationPort","Protocol","Observations","FirstSeen","LastSeen"};
            string[] auditCols={"File","Result","Rows","Accepted","Duplicate","ReverseOrOutside","Invalid","Unsupported"};
            string[] deltaCols={"IP","Port","Protocol","Earlier","Later","Change"};
            string[] sourceDeltaCols={"Router","SourceIP","DestinationIP","DestinationPort","Protocol","Earlier","Later","Change"};
            SaveCsv(Path.Combine(folder,"Office_IP_Port.csv"),endpointCols,a.Endpoints.Cast<object>());
            SaveCsv(Path.Combine(folder,"Office_Source_Rules.csv"),sourceCols,a.Sources.Cast<object>());
            SaveCsv(Path.Combine(folder,"Import_Audit.csv"),auditCols,a.Audit.Cast<object>());
            var sheets=new List<Sheet>{
                new Sheet("IP port protocol",endpointCols,a.Endpoints.Cast<object>().ToList()),
                new Sheet("Source rules",sourceCols,a.Sources.Cast<object>().ToList()),
                new Sheet("Import audit",auditCols,a.Audit.Cast<object>().ToList()),
                new Sheet("Read me",new[]{"Topic","Purpose"},new List<object>{
                    new Note("Target",a.Target),
                    new Note("Direction","Original destination in office /24 and original source outside it; translated addresses are not independent flows."),
                    new Note("Coverage","Observed NAT only; check original router status files, sampling time and VPN/LAN coverage."),
                    new Note("TCP","A connection from this PC confirms reachability at check time only, not service identity or business need."),
                    new Note("UDP","Observation only; TCP connect is never used to validate UDP."),
                    new Note("Review","No automatic firewall changes or automatic allow/deny decisions. Port hints are not service identification."),
                    new Note("Comparison","Not observed later does not mean disconnected or safe to block. Compare equivalent coverage and time windows.")
                })
            };
            if(a.Changes.Count>0||a.SourceChanges.Count>0)
            {
                SaveCsv(Path.Combine(folder,"Office_Changes.csv"),deltaCols,a.Changes.Cast<object>());
                SaveCsv(Path.Combine(folder,"Office_Source_Changes.csv"),sourceDeltaCols,a.SourceChanges.Cast<object>());
                sheets.Add(new Sheet("Endpoint changes",deltaCols,a.Changes.Cast<object>().ToList()));
                sheets.Add(new Sheet("Source changes",sourceDeltaCols,a.SourceChanges.Cast<object>().ToList()));
            }
            string excel=Path.Combine(folder,"Office_Access_Analysis.xlsx");
            try{SaveXlsx(excel,sheets,CancellationToken.None);return excel;}
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){try{File.Delete(excel);}catch{}return "";}
        }

        internal static string SaveBatchOfficeReport(string folder,List<Entry> rows,IEnumerable<BatchRouterStatus> statuses)
        {
            string[] statusCols = { "Router", "Status", "OfficeRows", "AllRows", "ReportFolder" };
            var statusRows=statuses.Cast<object>().ToList();
            SaveCsv(Path.Combine(folder,"07_Office_To_Connections.csv"),AllCols,rows.Cast<object>());
            SaveCsv(Path.Combine(folder,"00_Router_Status.csv"),statusCols,statusRows);
            string excel=Path.Combine(folder,"Office_Connections_All_Routers.xlsx");
            var sheets=new List<Sheet> {
                new Sheet("To office",AllCols,rows.Cast<object>().ToList()),
                new Sheet("Router status",statusCols,statusRows),
                new Sheet("Read me",new[]{"Topic","Purpose"},new List<object>{
                    new Note("Target",TargetSubnet+"; original DestinationIP only."),
                    new Note("Repeat","Original per-router observation flags; rows are not deleted."),
                    new Note("Coverage","Check Router status. Failed, cancelled and partial routers mean incomplete coverage. Zero matches does not prove absence of VPN/LAN traffic."),
                    new Note("Privacy","Internal network data; keep private.")
                })
            };
            // Preserve CSV if the workbook exceeds Excel limits or cannot be written.
            try { SaveXlsx(excel,sheets,CancellationToken.None); return excel; }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { try { File.Delete(excel); } catch { } return ""; }
        }

        internal static void SaveReport(CollectionResult r, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            SaveCsv(Path.Combine(r.Folder, "01_All_Connections.csv"), AllCols, r.All.Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "02_Target_Connections.csv"), AllCols, r.Target.Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "02_Other_Connections.csv"), AllCols, r.All.Where(x => x.TargetMatch != "YES").Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "03_Source_To_Destination.csv"), RouteCols, r.Routes.Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "04_Clients.csv"), HostCols, r.Hosts.Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "05_Office_Related.csv"), AllCols, r.OfficeRelated.Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "06_Office_Related_Links.csv"), OfficeRouteCols, r.OfficeRelatedRoutes.Cast<object>());
            SaveCsv(Path.Combine(r.Folder, "07_Office_To_Connections.csv"), AllCols, r.Target.Cast<object>());
            // Each run has its own folder; partial filenames also have a unique suffix.
            string excelPath = r.Partial
                ? Path.Combine(r.Folder, "Partial_Report_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture)
                    + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".xlsx")
                : Path.Combine(r.Folder, "Keenetic_Network_Connections.xlsx");
            var sheets = new List<Sheet> {
                new Sheet("All connections", AllCols, r.All.Cast<object>().ToList()),
                new Sheet("Target office", AllCols, r.Target.Cast<object>().ToList()),
                new Sheet("Office related", AllCols, r.OfficeRelated.Cast<object>().ToList()),
                new Sheet("Office links", OfficeRouteCols, r.OfficeRelatedRoutes.Cast<object>().ToList()),
                new Sheet("Source to destination", RouteCols, r.Routes.Cast<object>().ToList()),
                new Sheet("Clients", HostCols, r.Hosts.Cast<object>().ToList()),
                new Sheet("Summary", new[] { "Metric", "Value" }, new List<object> {
                    new Note("Run mode", r.Imported ? "IMPORTED CENTRAL FLOW CSV" : r.Demo ? "DEMO" : r.VpnRemote ? "REMOTE VPN / existing route" : "LIVE"),
                    new Note("Evidence source", r.Imported ? "CENTRAL_FLOW_CSV (external, independently supplied)" : "KEENETIC_NAT"),
                    new Note("Skipped/unparsed candidate rows", r.SkippedRecords.ToString(CultureInfo.InvariantCulture)),
                    new Note("Collector clock zone", DateTimeOffset.Now.ToString("zzz", CultureInfo.InvariantCulture) + " (local Windows clock; imported time values preserved as supplied)"),
                    new Note("VPN configured by app", "NO"),
                    new Note("Target subnet", TargetSubnet),
                    new Note(r.Imported ? "Imported flow observations" : "NAT observations", r.All.Count.ToString()),
                    new Note("Target observations", r.Target.Count.ToString()),
                    new Note("Office related observations (all fields)", r.OfficeRelated.Count.ToString()),
                    new Note("Raw office IP tokens in NAT JSON (NOT connections)", r.RawOfficeReferences.ToString()),
                    new Note("IP route endpoint", r.RouteCheck),
                    new Note("Interface endpoint", r.InterfaceCheck),
                    new Note("Other observations", (r.All.Count - r.Target.Count).ToString()),
                    new Note("Unique target links", r.Routes.Count.ToString()),
                    new Note("Known clients", r.Hosts.Count.ToString()),
                    new Note("Status", r.Status),
                    new Note("Partial report", r.Partial ? "YES" : "NO"),
                    new Note("Hostnames unavailable", r.HostnamesMissing ? "YES" : "NO")
                }),
                new Sheet("Read me", new[] { "Topic", "Purpose" }, new List<object> {
                    new Note("Mode", r.Imported ? "OFFLINE IMPORT - pre-existing central log; no router requests" : r.Demo ? "DEMO - synthetic, no router requests" : r.VpnRemote ? "REMOTE VPN - existing routed access, same read-only RCI API" : "LIVE - Keenetic API read-only"),
                    new Note("VPN", "The VPN must already exist and allow RCI management IP/port. TCP reachability does not prove VPN routing."),
                    new Note("Target", "ONLY original DestinationIP inside " + TargetSubnet + "; confirmed NAT observation, not all VPN traffic."),
                    new Note("Office related", "Office IP in ANY of SourceIP/DestinationIP/TranslatedSource/TranslatedDestination; NOT necessarily directed TO office."),
                    new Note("Office direction", "TO_OFFICE means original DestinationIP matches; FROM_OFFICE means original SourceIP matches; NAT_REFERENCE_REVIEW needs human review."),
                    new Note("Route and interfaces", "GET /rci/show/ip/route + GET /rci/show/interface are optional read-only context, NOT connection records."),
                    new Note("Missing office", "Zero matching NAT records does not establish that VPN office traffic is absent."),
                    new Note("Repeat", "YES when a five-tuple appears multiple times; rows retained. ObservationCount / FirstSeen / LastSeen per row."),
                    new Note("Partial", "If a run stops/fails after any valid snapshot, Partial_Report_*.xlsx and CSV are saved."),
                    new Note("Limitation", "NAT snapshots are not full packet capture. Wired LAN-to-LAN / VPN / IPv6 may be missing."),
                    new Note("Source", "KEENETIC_NAT rows are router-observed; CENTRAL_FLOW_CSV rows are from manually supplied logs; never infer missing records."),
                    new Note("Security", "Local reports contain internal network data. Keep private.")
                })
            };
            SaveXlsx(excelPath, sheets, token);
            r.Excel = excelPath;
            File.WriteAllText(Path.Combine(r.Folder, "SUMMARY.txt"),
                (r.Imported ? "IMPORT (existing central-flow CSV; offline, no router access)" : r.Demo ? "DEMO (synthetic)" : r.VpnRemote ? "REMOTE VPN (existing route, read-only)" : "LIVE (read-only)") + Environment.NewLine +
                "Evidence source: " + (r.Imported ? "CENTRAL_FLOW_CSV" : "KEENETIC_NAT") + Environment.NewLine +
                "Skipped/unparsed candidate records: " + r.SkippedRecords + Environment.NewLine +
                "Collector local UTC offset: " + DateTimeOffset.Now.ToString("zzz", CultureInfo.InvariantCulture) + Environment.NewLine +
                "Target: " + TargetSubnet + Environment.NewLine +
                "All observed/imported rows: " + r.All.Count + Environment.NewLine +
                "Target observations (original DestinationIP): " + r.Target.Count + Environment.NewLine +
                "Office related observations (ANY IP field): " + r.OfficeRelated.Count + Environment.NewLine +
                "Raw office IP mentions in NAT JSON (NOT verified connections): " + r.RawOfficeReferences + Environment.NewLine +
                "Route metadata endpoint: " + r.RouteCheck + Environment.NewLine +
                "Interface metadata endpoint: " + r.InterfaceCheck + Environment.NewLine +
                "Unique source/destination/protocol/port links: " + r.Routes.Count + Environment.NewLine +
                "Clients: " + r.Hosts.Count + Environment.NewLine +
                "Status: " + r.Status + Environment.NewLine +
                "Partial: " + (r.Partial ? "YES" : "NO") + Environment.NewLine +
                "WARNING: Snapshots do not cover all LAN/VPN connections. Live compatibility is unverified." + Environment.NewLine,
                new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(r.Folder, "OFFICE_VISIBILITY.txt"),
                "KEENETIC OBSERVER v1.8 OFFICE VISIBILITY REPORT" + Environment.NewLine +
                "Evidence source: " + (r.Imported ? "CENTRAL_FLOW_CSV (supplied CSV evidence)" : "KEENETIC_NAT (router API)") + Environment.NewLine +
                "Skipped/unparsed candidate records: " + r.SkippedRecords + Environment.NewLine +
                "Only target subnet: " + TargetSubnet + Environment.NewLine +
                "Recorded observations: " + r.All.Count + Environment.NewLine +
                "TO OFFICE (original DestinationIP): " + r.Target.Count + Environment.NewLine +
                "OFFICE RELATED (any of four IP fields): " + r.OfficeRelated.Count + Environment.NewLine +
                "Office references in RAW NAT JSON (not verified flows): " + r.RawOfficeReferences + Environment.NewLine +
                "Route metadata: " + r.RouteCheck + Environment.NewLine +
                "Interface metadata: " + r.InterfaceCheck + Environment.NewLine +
                (r.Imported ? "Evidence comes from an already existing centrally exported CSV; accuracy depends on its origin and columns." : "Evidence for per-device source/destination/port comes ONLY from parsed NAT observations.") + Environment.NewLine +
                "show ip route and show interface reveal network context, NOT all active flows." + Environment.NewLine +
                "No target entries DOES NOT prove that no application exchanges data with the target network over the VPN." + Environment.NewLine +
                "If known active connections are missing, request an authorized read-only flow report from the central VPN gateway or office server." + Environment.NewLine +
                "Do not expose router management ports publicly. Protect Raw_JSON files as confidential network data." + Environment.NewLine,
                new UTF8Encoding(true));
        }
        private sealed class Note { public string Topic, Purpose; public Note(string a, string b) { Topic = a; Purpose = b; } }
        private sealed class Sheet
        {
            public string Name; public string[] Columns; public List<object> Rows;
            public Sheet(string n, string[] cols, List<object> rows) { Name = n; Columns = cols; Rows = rows; }
        }
        private static string Value(object obj, string name)
        {
            if (obj is Note && name == "Metric") name = "Topic";
            if (obj is Note && name == "Value") name = "Purpose";
            var fi = obj.GetType().GetField(name);
            object v = fi == null ? null : fi.GetValue(obj);
            return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
        }
        private static string SafeCell(string s)
        {
            if (s == null) return "";
            return Regex.Replace(s, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        }
        private static string SafeCsvValue(string s)
        {
            s = SafeCell(s);
            if (s.TrimStart().Length > 0 && "=+-@".IndexOf(s.TrimStart()[0]) >= 0) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        private static void SaveCsv(string path, string[] cols, IEnumerable<object> rows)
        {
            using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(";", cols.Select(SafeCsvValue).ToArray()));
                foreach (object row in rows) writer.WriteLine(string.Join(";", cols.Select(c => SafeCsvValue(Value(row, c))).ToArray()));
            }
        }
        private static void ZipText(ZipArchive zip, string name, string text)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream s = entry.Open()) using (StreamWriter writer = new StreamWriter(s, new UTF8Encoding(false))) writer.Write(text);
        }
        private static string Xml(string s) { return System.Security.SecurityElement.Escape(SafeCell(s)) ?? ""; }
        private static string SafeExcelCell(string s)
        {
            s = SafeCell(s);
            if (s.Length <= 32767) return s;
            int limit = 32764;
            if (char.IsHighSurrogate(s[limit - 1])) limit--;
            return s.Substring(0, limit) + "...";
        }
        private static string ExcelColumn(int num)
        {
            string value = "";
            while (num > 0) { num--; value = (char)('A' + num % 26) + value; num /= 26; }
            return value;
        }
        // OpenXML styles: dark header, zebra rows, cyan targets, amber repeats, muted non-targets.
        private static string StyledWorkbookStyles()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?><styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
<fonts count=""5""><font><sz val=""11""/><name val=""Calibri""/><color rgb=""FF243A45""/></font><font><b/><sz val=""11""/><name val=""Calibri""/><color rgb=""FFFFFFFF""/></font><font><b/><sz val=""11""/><name val=""Calibri""/><color rgb=""FF006E73""/></font><font><b/><sz val=""11""/><name val=""Calibri""/><color rgb=""FF775500""/></font><font><sz val=""11""/><name val=""Calibri""/><color rgb=""FF7C8791""/></font></fonts>
<fills count=""8""><fill><patternFill patternType=""none""/></fill><fill><patternFill patternType=""gray125""/></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FF10252D""/><bgColor indexed=""64""/></patternFill></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FFF4F9FA""/><bgColor indexed=""64""/></patternFill></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FFFFFFFF""/><bgColor indexed=""64""/></patternFill></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FFD7F5F1""/><bgColor indexed=""64""/></patternFill></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FFFFEDC1""/><bgColor indexed=""64""/></patternFill></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FFE9EEF1""/><bgColor indexed=""64""/></patternFill></fill></fills>
<borders count=""2""><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top style=""hair""><color rgb=""FFDCE8EA""/></top><bottom style=""hair""><color rgb=""FFDCE8EA""/></bottom><diagonal/></border></borders>
<cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
<cellXfs count=""8""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/><xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0"" applyFill=""1"" applyFont=""1"" applyAlignment=""1""><alignment vertical=""center""/></xf><xf numFmtId=""0"" fontId=""0"" fillId=""3"" borderId=""1"" xfId=""0"" applyFill=""1""/><xf numFmtId=""0"" fontId=""0"" fillId=""4"" borderId=""1"" xfId=""0"" applyFill=""1""/><xf numFmtId=""0"" fontId=""2"" fillId=""5"" borderId=""1"" xfId=""0"" applyFill=""1""/><xf numFmtId=""0"" fontId=""3"" fillId=""6"" borderId=""1"" xfId=""0"" applyFill=""1""/><xf numFmtId=""0"" fontId=""4"" fillId=""7"" borderId=""1"" xfId=""0"" applyFill=""1""/><xf numFmtId=""0"" fontId=""2"" fillId=""3"" borderId=""1"" xfId=""0"" applyFill=""1""/></cellXfs>
<cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles></styleSheet>";
        }

        private static void SaveXlsx(string filename, List<Sheet> sheets, CancellationToken token)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                string types = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>";
                for (int i = 1; i <= sheets.Count; i++) types += "<Override PartName=\"/xl/worksheets/sheet" + i + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>";
                ZipText(zip, "[Content_Types].xml", types + "</Types>");
                ZipText(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                string workbook = "<?xml version=\"1.0\" encoding=\"utf-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>";
                string relationships = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">";
                for (int i = 1; i <= sheets.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    Sheet sheet = sheets[i - 1];
                    workbook += "<sheet name=\"" + Xml(sheet.Name) + "\" sheetId=\"" + i + "\" r:id=\"rId" + i + "\"/>";
                    relationships += "<Relationship Id=\"rId" + i + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + i + ".xml\"/>";
                    // Excel sheet row cap: detect rather than silently truncate.
                    if (sheet.Rows.Count > 1048575) throw new InvalidDataException("Слишком много строк для XLSX (лимит Excel 1 048 576).");
                    ZipArchiveEntry entry = zip.CreateEntry("xl/worksheets/sheet" + i + ".xml", CompressionLevel.Optimal);
                    using (Stream stream = entry.Open())
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"18\"/><cols>");
                        for (int col = 0; col < sheet.Columns.Length; col++)
                        {
                            string header = sheet.Columns[col];
                            int longest = Math.Min(42, Math.Max(header.Length + 3, 13));
                            foreach (object item in sheet.Rows.Take(400)) longest = Math.Max(longest, Math.Min(42, Value(item, header).Length + 2));
                            // Keep timestamps, names and IPs readable without extreme widths.
                            if (header == "Timestamp" || header == "FirstSeen" || header == "LastSeen") longest = Math.Max(longest, 21);
                            if (header == "Note" || header == "Purpose" || header == "Value") longest = Math.Min(65, Math.Max(longest, 36));
                            writer.Write("<col min=\"" + (col+1) + "\" max=\"" + (col+1) + "\" width=\"" + longest + "\" customWidth=\"1\"/>");
                        }
                        writer.Write("</cols><sheetData>");
                        for (int row = 0; row <= sheet.Rows.Count; row++)
                        {
                            token.ThrowIfCancellationRequested();
                            writer.Write("<row r=\"" + (row + 1) + "\" ht=\"" + (row == 0 ? 30 : 22) + "\" customHeight=\"1\">");
                            for (int col = 0; col < sheet.Columns.Length; col++)
                            {
                                string value = row == 0 ? sheet.Columns[col] : Value(sheet.Rows[row - 1], sheet.Columns[col]);
                                int style = row == 0 ? 1 : (row % 2 == 0 ? 2 : 3);
                                if (row != 0 && sheet.Rows[row-1] is Entry)
                                {
                                    Entry record = (Entry)sheet.Rows[row-1];
                                    if (record.TargetMatch == "YES") style = 4;
                                    if (record.Repeat == "YES" && (sheet.Columns[col] == "Repeat" || record.TargetMatch != "YES")) style = 5;
                                    if (sheet.Columns[col] == "TargetMatch" && record.TargetMatch == "NO") style = 6;
                                }
                                if(row!=0 && sheet.Columns[col] is "Status" or "TcpStatus" or "Change" or "Result")
                                {
                                    if(value is "TCP OPEN" or "COMPLETE" or "IMPORTED" or "OBSERVED BOTH")style=4;
                                    else if(value.Contains("FAILED")||value.Contains("ERROR")||value.Contains("REFUSED")||value.Contains("TIMEOUT"))style=5;
                                    else if(value.Contains("PARTIAL")||value.Contains("NOT OBSERVED")||value.Contains("NOT CHECKED")||value.Contains("UDP OBSERVED"))style=6;
                                }
                                if (row != 0 && sheet.Name == "Summary") style = 7;
                                writer.Write("<c r=\"" + ExcelColumn(col + 1) + (row + 1) + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + Xml(SafeExcelCell(value)) + "</t></is></c>");
                            }
                            writer.Write("</row>");
                        }
                        writer.Write("</sheetData><autoFilter ref=\"A1:" + ExcelColumn(sheet.Columns.Length) + (sheet.Rows.Count + 1) + "\"/></worksheet>");
                    }
                }
                relationships += "<Relationship Id=\"rId" + (sheets.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
                ZipText(zip, "xl/workbook.xml", workbook + "</sheets></workbook>");
                ZipText(zip, "xl/_rels/workbook.xml.rels", relationships);
                ZipText(zip, "xl/styles.xml", StyledWorkbookStyles());
            }
        }
    }

    // VISUAL SYSTEM v1.5.1 — black liquid-glass / cyan HUD.
    // Drawn in WinForms/GDI+ so all existing controls remain genuine, interactive controls.
}
