// Keenetic Observer 1.9.1: offline Network Analytics exports. Never sends network requests.
// A neutral, artifact_tool-generated XLSX design is embedded; this writer injects only measured data.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace KeeneticObserver
{
    internal sealed class TrafficPrevious
    {
        public string Folder = "";
        public Dictionary<string, TrafficRow> Rows = new Dictionary<string, TrafficRow>(StringComparer.OrdinalIgnoreCase);
    }
    internal sealed class TrafficCompareLine
    {
        public string Key = "";
        public TrafficRow Current, Previous;
        public string Status = "";
    }

    internal static class TrafficAnalyticsExport
    {
        private const double MiB = 1048576d;
        private const double GiB = 1073741824d;
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace ChartNs = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private static string Key(TrafficRow item)
        {
            if (item == null) return "";
            string mac = (item.MAC ?? "").Trim().ToLowerInvariant();
            if (mac.Length > 0 && mac != "—" && mac != "-" && mac != "00:00:00:00:00:00") return "MAC:" + mac;
            string ip = (item.IP ?? "").Trim();
            if (ip.Length > 0 && ip != "—" && ip != "0.0.0.0") return "IP:" + ip;
            return "NAME:" + (item.Name ?? "").Trim().ToLowerInvariant();
        }
        private static double? ParseUnits(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Trim() == "неизвестно") return null;
            string[] bits = value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length != 2) return null;
            double n;
            if (!Double.TryParse(bits[0], NumberStyles.Float, Invariant, out n) || n < 0) return null;
            double factor;
            switch (bits[1])
            {
                case "B": factor = 1d / MiB; break;
                case "KiB": factor = 1d / 1024d; break;
                case "MiB": factor = 1d; break;
                case "GiB": factor = 1024d; break;
                case "TiB": factor = 1048576d; break;
                default: return null;
            }
            return n * factor;
        }
        private static double? Download(TrafficRow row) { return row.DownloadBytes.HasValue ? (double?)((double)row.DownloadBytes.Value / MiB) : ParseUnits(row.Download); }
        private static double? Upload(TrafficRow row) { return row.UploadBytes.HasValue ? (double?)((double)row.UploadBytes.Value / MiB) : ParseUnits(row.Upload); }
        private static double? UploadRatio(TrafficRow row)
        {
            double? down = Download(row), up = Upload(row);
            if (!down.HasValue || !up.HasValue || down.Value + up.Value <= 0) return null;
            return up.Value / (down.Value + up.Value);
        }
        private static string Csv(string text)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 0 && "=+-@\t\r".IndexOf(text[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        private static string Number(double value) { return value.ToString("0.#####", Invariant); }
        private static string NumOrBlank(double? value) { return value.HasValue ? Number(value.Value) : ""; }

        // RFC-4180-style quoted CSV input. Used only for our own previous exported file.
        private static List<string[]> ParseCsv(string raw)
        {
            List<string[]> rows = new List<string[]>();
            List<string> fields = new List<string>();
            StringBuilder part = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\uFEFF' && i == 0) continue;
                if (c == '"')
                {
                    if (quoted && i + 1 < raw.Length && raw[i + 1] == '"') { part.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { fields.Add(part.ToString()); part.Length = 0; }
                else if ((c == '\n' || c == '\r') && !quoted)
                {
                    if (c == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n') i++;
                    fields.Add(part.ToString()); part.Length = 0;
                    if (fields.Any(x => x.Length > 0)) rows.Add(fields.ToArray());
                    fields = new List<string>();
                }
                else part.Append(c);
            }
            if (quoted) throw new InvalidDataException("Incomplete quoted CSV field.");
            fields.Add(part.ToString());
            if (fields.Any(x => x.Length > 0)) rows.Add(fields.ToArray());
            return rows;
        }
        private static string Field(string[] columns, Dictionary<string, int> header, string key)
        {
            int index;
            return header.TryGetValue(key, out index) && index >= 0 && index < columns.Length ? columns[index] : "";
        }
        internal static TrafficPrevious FindPrevious(string folder, string routerIp, string window)
        {
            string root = Path.GetDirectoryName(folder), prefix = "TRAFFIC_Router_" + routerIp.Replace('.', '_').Replace(':', '_') + "_";
            if (String.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;
            // Sort by ISO timestamp in folder name; current folder must not be reused as baseline.
            foreach (string path in Directory.GetDirectories(root, prefix + "*").OrderByDescending(x => Path.GetFileName(x), StringComparer.Ordinal))
            {
                if (String.Equals(Path.GetFullPath(path), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)) continue;
                if (String.Compare(Path.GetFileName(path), Path.GetFileName(folder), StringComparison.Ordinal) >= 0) continue;
                string file = Path.Combine(path, "08_Top_Traffic.csv");
                if (!File.Exists(file)) continue;
                try
                {
                    List<string[]> rows = ParseCsv(File.ReadAllText(file, Encoding.UTF8));
                    if (rows.Count < 1) continue;
                    Dictionary<string, int> header = rows[0].Select((x, i) => new { Column = x, Index = i })
                        .GroupBy(x => x.Column, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(x => x.Key, x => x.First().Index, StringComparer.OrdinalIgnoreCase);
                    if (!header.ContainsKey("TotalBytes") || !header.ContainsKey("MAC") || !header.ContainsKey("Window")) continue;
                    TrafficPrevious previous = new TrafficPrevious { Folder = path };
                    bool matching = true;
                    foreach (string[] columns in rows.Skip(1))
                    {
                        if (!String.Equals(Field(columns, header, "Window"), window, StringComparison.Ordinal)) { matching = false; break; }
                        long bytes;
                        if (!Int64.TryParse(Field(columns, header, "TotalBytes"), NumberStyles.Integer, Invariant, out bytes) || bytes < 0) continue;
                        TrafficRow row = new TrafficRow { Name = Field(columns, header, "Name"), IP = Field(columns, header, "IP"),
                            MAC = Field(columns, header, "MAC"), TotalBytes = bytes };
                        string key = Key(row);
                        if (!previous.Rows.ContainsKey(key) || previous.Rows[key].TotalBytes < bytes) previous.Rows[key] = row;
                    }
                    if (matching && previous.Rows.Count > 0) return previous;
                }
                catch (IOException) { }
                catch (InvalidDataException) { }
                catch (ArgumentException) { }
            }
            return null;
        }
        private static List<TrafficCompareLine> Compare(IList<TrafficRow> current, TrafficPrevious previous)
        {
            Dictionary<string, TrafficRow> now = new Dictionary<string, TrafficRow>(StringComparer.OrdinalIgnoreCase);
            foreach (TrafficRow row in current)
            {
                string key = Key(row);
                if (!now.ContainsKey(key) || now[key].TotalBytes < row.TotalBytes) now[key] = row;
            }
            HashSet<string> keys = new HashSet<string>(now.Keys, StringComparer.OrdinalIgnoreCase);
            if (previous != null) keys.UnionWith(previous.Rows.Keys);
            List<TrafficCompareLine> result = new List<TrafficCompareLine>();
            foreach (string key in keys)
            {
                TrafficRow a, b;
                now.TryGetValue(key, out a);
                b = null;
                if (previous != null) previous.Rows.TryGetValue(key, out b);
                result.Add(new TrafficCompareLine { Key = key, Current = a, Previous = b,
                    Status = previous == null ? "НЕТ БАЗЫ" : a == null ? "НЕ В ТЕКУЩЕМ СРЕЗЕ" : b == null ? "НОВОЕ УСТРОЙСТВО" :
                        a.TotalBytes > b.TotalBytes ? "ВЫШЕ" : a.TotalBytes < b.TotalBytes ? "НИЖЕ" : "БЕЗ ИЗМЕНЕНИЙ" });
            }
            return result.OrderByDescending(x => x.Current == null ? 0 : x.Current.TotalBytes).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        }
        private static string[] Flags(TrafficRow row)
        {
            List<string> flags = new List<string>();
            if (row.IP == "0.0.0.0" || row.IP == "—" || String.IsNullOrWhiteSpace(row.IP)) flags.Add("NO_CURRENT_IP");
            double? share = UploadRatio(row);
            if (share.HasValue && row.TotalBytes >= 20L * 1048576L && share.Value >= .9) flags.Add("HIGH_UPLOAD_SHARE");
            if (!Download(row).HasValue || !Upload(row).HasValue) flags.Add("PARTIAL_DIRECTION_COUNTERS");
            return flags.ToArray();
        }
        private static void SaveComparison(string file, IEnumerable<TrafficCompareLine> lines, string routerIp, string window, DateTimeOffset readTime, TrafficPrevious previous)
        {
            using (StreamWriter wr = new StreamWriter(file, false, new UTF8Encoding(true)))
            {
                wr.WriteLine("Router,CollectedAt,Window,MAC,IP,Name,PreviousMiB,CurrentMiB,DeltaMiB,DeltaPct,Status,PreviousRunFolder");
                foreach (TrafficCompareLine line in lines)
                {
                    TrafficRow row = line.Current ?? line.Previous;
                    double? old = line.Previous == null ? (double?)null : (double)line.Previous.TotalBytes / MiB;
                    double? now = line.Current == null ? (double?)null : (double)line.Current.TotalBytes / MiB;
                    double? delta = old.HasValue && now.HasValue ? now.Value - old.Value : (double?)null;
                    double? percent = old.HasValue && old.Value > 0 && now.HasValue ? (now.Value - old.Value) / old.Value * 100d : (double?)null;
                    wr.WriteLine(String.Join(",", new[] { Csv(routerIp), Csv(readTime.ToString("o", Invariant)), Csv(window), Csv(row.MAC), Csv(row.IP), Csv(row.Name),
                        NumOrBlank(old), NumOrBlank(now), NumOrBlank(delta), NumOrBlank(percent), Csv(line.Status), Csv(previous == null ? "" : Path.GetFileName(previous.Folder)) }));
                }
            }
        }
        private static void SaveFlags(string file, IEnumerable<TrafficRow> rows, string routerIp, DateTimeOffset readTime)
        {
            using (StreamWriter wr = new StreamWriter(file, false, new UTF8Encoding(true)))
            {
                wr.WriteLine("Router,CollectedAt,Name,IP,MAC,TotalMiB,UploadSharePct,Flag,Meaning");
                foreach (TrafficRow row in rows)
                    foreach (string flag in Flags(row))
                        wr.WriteLine(String.Join(",", new[] { Csv(routerIp), Csv(readTime.ToString("o", Invariant)), Csv(row.Name), Csv(row.IP), Csv(row.MAC),
                            Number((double)row.TotalBytes / MiB), NumOrBlank(UploadRatio(row).HasValue ? UploadRatio(row) * 100 : null), Csv(flag),
                            Csv(flag == "HIGH_UPLOAD_SHARE" ? "Требует контекста; не доказательство инцидента" :
                                flag == "NO_CURRENT_IP" ? "Нет текущего адреса в статистике" : "Нет раздельных счётчиков в ответе") }));
            }
        }
        // Sheet XML cell cache; all styled template rows are deliberately created in advance.
        private sealed class Cells
        {
            private readonly Dictionary<string, XElement> map;
            internal Cells(XDocument doc)
            {
                map = doc.Descendants(Ns + "c").Where(x => x.Attribute("r") != null)
                    .ToDictionary(x => (string)x.Attribute("r"), x => x, StringComparer.OrdinalIgnoreCase);
            }
            internal void Text(string address, string value)
            {
                XElement cell;
                if (!map.TryGetValue(address, out cell)) throw new InvalidDataException("Missing cell " + address + " in Excel template.");
                cell.RemoveNodes(); cell.SetAttributeValue("t", "inlineStr");
                cell.Add(new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"),
                    (value ?? "").Length > 32767 ? value.Substring(0, 32767) : value)));
            }
            internal void Num(string address, double? value)
            {
                XElement cell;
                if (!map.TryGetValue(address, out cell)) throw new InvalidDataException("Missing cell " + address + " in Excel template.");
                cell.RemoveNodes(); cell.SetAttributeValue("t", null);
                if (value.HasValue && !Double.IsNaN(value.Value) && !Double.IsInfinity(value.Value))
                    cell.Add(new XElement(Ns + "v", value.Value.ToString("R", Invariant)));
            }
        }
        private static string Column(int index)
        {
            string value = "";
            for (int n = index + 1; n > 0; n = (n - 1) / 26) value = (char)('A' + (n - 1) % 26) + value;
            return value;
        }
        private static void Data(Cells cells, int row, params object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string address = Column(i) + row.ToString(Invariant);
                if (values[i] is double) cells.Num(address, (double)values[i]);
                else if (values[i] is int) cells.Num(address, Convert.ToDouble(values[i], Invariant));
                else if (values[i] is long) cells.Num(address, Convert.ToDouble(values[i], Invariant));
                else if (values[i] == null) cells.Num(address, null);
                else cells.Text(address, Convert.ToString(values[i], Invariant));
            }
        }
        private static void PatchSheet1(XDocument doc, TrafficResult data, string ip, DateTimeOffset readTime)
        {
            Cells c = new Cells(doc);
            long totalBytes = data.Rows.Sum(x => x.TotalBytes);
            double download = data.Rows.Sum(x => Download(x) ?? 0d), upload = data.Rows.Sum(x => Upload(x) ?? 0d);
            c.Text("B4", "KEENETIC OBSERVER  •  TRAFFIC MONITOR / " + data.Window + "  •  ROUTER " + ip);
            c.Text("B5", "ОБЗОР ПОТРЕБЛЕНИЯ   /   " + readTime.ToString("yyyy-MM-dd HH:mm:ss zzz", Invariant));
            c.Num("B9", totalBytes / GiB); c.Num("E9", download); c.Num("I9", upload); c.Num("L9", data.Rows.Count);
            for (int i = 0; i < 10; i++)
            {
                TrafficRow r = i < data.Rows.Count ? data.Rows[i] : null;
                int row = 15 + i;
                c.Num("B" + row, r == null ? (double?)null : i + 1);
                c.Text("C" + row, r == null ? "" : r.Name);
                c.Text("D" + row, r == null ? "" : r.IP);
                c.Num("E" + row, r == null ? (double?)null : (double)r.TotalBytes / MiB);
                c.Num("F" + row, r == null ? (double?)null : UploadRatio(r));
                c.Num("G" + row, r == null || totalBytes <= 0 ? (double?)null : (double)r.TotalBytes / totalBytes);
            }
            int active = data.Rows.Count(x => x.TotalBytes > 0), missingIp = data.Rows.Count(x => x.IP == "0.0.0.0" || x.IP == "—");
            int uploadFlags = data.Rows.Count(x => Flags(x).Contains("HIGH_UPLOAD_SHARE"));
            long top7 = data.Rows.Take(7).Sum(x => x.TotalBytes);
            c.Text("B28", "•  " + active + " из " + data.Rows.Count + " устройств с учтённым трафиком");
            c.Text("B29", "•  TOP 7 формируют " + (totalBytes <= 0 ? "—" : (100d * top7 / totalBytes).ToString("0.0", Invariant) + "%") + " учтённого объёма");
            c.Text("B30", "•  " + uploadFlags + " устройств: высокая доля отправки ≥90% при общем объёме ≥20 MiB");
            c.Text("B31", "•  " + missingIp + " устройств без текущего IP в статистике");
            c.Text("B39", "NETWORK ANALYTICS  //  READ-ONLY  •  " + data.Rows.Count + " RECORDS  •  RRD " + data.Window);
        }
        private static void PatchSheet2(XDocument doc, TrafficResult data, DateTimeOffset readTime)
        {
            Cells c = new Cells(doc); long total = data.Rows.Sum(x => x.TotalBytes);
            c.Text("B4", data.Rows.Count + " УСТРОЙСТВ  •  " + data.Window + "  •  СОРТИРОВКА ПО TOTAL BYTES  •  " + readTime.ToString("yyyy-MM-dd HH:mm zzz", Invariant));
            for (int i = 0; i < 200; i++)
            {
                TrafficRow row = i < data.Rows.Count ? data.Rows[i] : null;
                int excel = 8 + i;
                if (row == null) { Data(c, excel, null, "", "", "", null, null, null, null, null, null, "", "", ""); continue; }
                Data(c, excel, i + 1, row.Name, row.IP, row.MAC, Download(row), Upload(row), (double)row.TotalBytes / MiB,
                    row.TotalBytes, UploadRatio(row), total > 0 ? (double?)((double)row.TotalBytes / total) : null,
                    row.Status, row.Window, row.IP == "0.0.0.0" ? "IP 0.0.0.0" : row.TotalBytes == 0 ? "Нет учтённого трафика" : "");
            }
            // Table filter covers populated rows only; preserve at least one blank data row for empty results.
            XElement filter = doc.Descendants(Ns + "autoFilter").FirstOrDefault();
            if (filter != null) filter.SetAttributeValue("ref", "A7:M" + Math.Max(8, data.Rows.Count + 7));
        }
        private static void PatchSheet3(XDocument doc, TrafficResult data)
        {
            Cells c = new Cells(doc);
            long total = data.Rows.Sum(x => x.TotalBytes);
            List<TrafficRow> high = data.Rows.Where(x => Flags(x).Contains("HIGH_UPLOAD_SHARE")).ToList();
            c.Text("B4", high.Count + " КАНДИДАТОВ ДЛЯ ПРОВЕРКИ  •  ОТДАЧА ≥90%  •  ВСЕГО ≥20 MiB  •  НЕ ПРИЗНАК ИНЦИДЕНТА");
            for (int i = 0; i < 200; i++)
            {
                TrafficRow r = i < high.Count ? high[i] : null;
                int excel = 8 + i;
                if (r == null) { Data(c, excel, null, "", "", null, null, null, null, null, ""); continue; }
                Data(c, excel, Int32.Parse(r.Rank, Invariant), r.Name, r.IP, Download(r), Upload(r), (double)r.TotalBytes / MiB,
                    UploadRatio(r), total > 0 ? (double?)((double)r.TotalBytes / total) : null, "Требует контекста");
            }
        }
        private static void PatchSheet4(XDocument doc, List<TrafficCompareLine> lines, TrafficPrevious previous, string window)
        {
            Cells c = new Cells(doc);
            c.Text("B4", previous == null ? "БАЗОВЫЙ СРЕЗ НЕ НАЙДЕН  •  СЛЕДУЮЩИЙ СБОР ПОЗВОЛИТ СРАВНИТЬ " + window :
                "СВЕРКА " + window + "  •  БАЗА: " + Path.GetFileName(previous.Folder) + "  •  СКОЛЬЗЯЩИЕ ОКНА");
            c.Text("A5", previous == null ? "Нет прошлого сбора того же роутера и периода. Будущие выгрузки будут сравниваться автоматически." :
                "Изменение двух скользящих RRD-окон не равно расходу между запусками. Исчезновение из выгрузки не доказывает отключение.");
            for (int i = 0; i < 400; i++)
            {
                TrafficCompareLine line = i < lines.Count ? lines[i] : null;
                int excel = 8 + i;
                if (line == null) { Data(c, excel, null, "", "", null, null, null, null, ""); continue; }
                TrafficRow r = line.Current ?? line.Previous;
                double? old = line.Previous == null ? (double?)null : (double)line.Previous.TotalBytes / MiB;
                double? now = line.Current == null ? (double?)null : (double)line.Current.TotalBytes / MiB;
                double? delta = old.HasValue && now.HasValue ? now.Value - old.Value : (double?)null;
                double? pct = old.HasValue && old.Value > 0d && now.HasValue ? (double?)((now.Value - old.Value) / old.Value) : null;
                Data(c, excel, i + 1, r.Name, r.IP, now, old, delta, pct, line.Status);
            }
        }
        private static void PatchSheet5(XDocument doc, TrafficResult data, string router, DateTimeOffset readTime, TrafficPrevious previous)
        {
            Cells c = new Cells(doc);
            c.Text("D7", "Router " + router);
            c.Text("D8", "08_Top_Traffic.csv — сбор Keenetic Observer");
            c.Text("D9", readTime.ToString("yyyy-MM-dd HH:mm:ss zzz", Invariant));
            c.Text("D10", data.Window + " (RRD, скользящее окно)");
            c.Text("D13", data.Rows.Count.ToString(Invariant));
            c.Text("D14", data.Rows.Sum(x => x.TotalBytes).ToString("N0", Invariant) + " bytes");
            c.Text("D21", previous == null ? "Нет прошлого сопоставимого сбора; CSV сверки отмечает НЕТ БАЗЫ." :
                "База: " + Path.GetFileName(previous.Folder) + "; RRD-окна могут пересекаться.");
        }
        private static void PatchChart(XDocument doc, TrafficResult result)
        {
            XElement strCache = doc.Descendants(ChartNs + "strCache").FirstOrDefault();
            XElement numCache = doc.Descendants(ChartNs + "numCache").FirstOrDefault();
            if (strCache == null || numCache == null) return;
            foreach (XElement pt in strCache.Elements(ChartNs + "pt").ToArray()) pt.Remove();
            foreach (XElement pt in numCache.Elements(ChartNs + "pt").ToArray()) pt.Remove();
            XElement stCount = strCache.Element(ChartNs + "ptCount"), numCount = numCache.Element(ChartNs + "ptCount");
            if (stCount != null) stCount.SetAttributeValue("val", Math.Min(10, result.Rows.Count));
            if (numCount != null) numCount.SetAttributeValue("val", Math.Min(10, result.Rows.Count));
            for (int i = 0; i < Math.Min(10, result.Rows.Count); i++)
            {
                strCache.Add(new XElement(ChartNs + "pt", new XAttribute("idx", i), new XElement(ChartNs + "v", result.Rows[i].Name)));
                numCache.Add(new XElement(ChartNs + "pt", new XAttribute("idx", i),
                    new XElement(ChartNs + "v", Number((double)result.Rows[i].TotalBytes / MiB))));
            }
        }
        private static void PatchTable(XDocument doc, int rowCount)
        {
            XElement table = doc.Descendants(Ns + "table").FirstOrDefault();
            if (table != null) table.SetAttributeValue("ref", "A7:M" + Math.Max(8, rowCount + 7));
        }
        private static void WriteExcel(string path, TrafficResult data, string router, DateTimeOffset time,
            List<TrafficCompareLine> changes, TrafficPrevious previous)
        {
            const string resource = "TrafficNetworkAnalyticsTemplate.xlsx";
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) throw new InvalidDataException("Missing neutral Excel design template: " + resource);
                using (ZipArchive source = new ZipArchive(stream, ZipArchiveMode.Read, true))
                using (ZipArchive dest = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    foreach (ZipArchiveEntry sourceEntry in source.Entries)
                    {
                        ZipArchiveEntry dst = dest.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                        using (Stream input = sourceEntry.Open()) using (Stream output = dst.Open())
                        {
                            bool modify = sourceEntry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) &&
                                sourceEntry.FullName.EndsWith(".xml", StringComparison.Ordinal) ||
                                sourceEntry.FullName == "xl/drawings/charts/chart1.xml" || sourceEntry.FullName == "xl/tables/table1.xml";
                            if (!modify) { input.CopyTo(output); continue; }
                            XDocument xml = XDocument.Load(input);
                            switch (sourceEntry.FullName)
                            {
                                case "xl/worksheets/sheet1.xml": PatchSheet1(xml, data, router, time); break;
                                case "xl/worksheets/sheet2.xml": PatchSheet2(xml, data, time); break;
                                case "xl/worksheets/sheet3.xml": PatchSheet3(xml, data); break;
                                case "xl/worksheets/sheet4.xml": PatchSheet4(xml, changes, previous, data.Window); break;
                                case "xl/worksheets/sheet5.xml": PatchSheet5(xml, data, router, time, previous); break;
                                case "xl/drawings/charts/chart1.xml": PatchChart(xml, data); break;
                                case "xl/tables/table1.xml": PatchTable(xml, data.Rows.Count); break;
                            }
                            xml.Save(output);
                        }
                    }
                }
            }
        }
        internal static void Export(TrafficResult result, string routerIp, DateTimeOffset readTime, Action<string> report)
        {
            TrafficPrevious previous = FindPrevious(result.Folder, routerIp, result.Window);
            List<TrafficCompareLine> diff = Compare(result.Rows, previous);
            string compareCsv = Path.Combine(result.Folder, "09_Traffic_Comparison.csv");
            string flagsCsv = Path.Combine(result.Folder, "10_Traffic_Flags.csv");
            SaveComparison(compareCsv, diff, routerIp, result.Window, readTime, previous);
            SaveFlags(flagsCsv, result.Rows, routerIp, readTime);
            string excel = Path.Combine(result.Folder, "Keenetic_Traffic_Network_Analytics.xlsx");
            WriteExcel(excel, result, routerIp, readTime, diff, previous);
            result.Excel = excel;
            report("NETWORK ANALYTICS // XLSX=" + excel + "; previous=" + (previous == null ? "NONE" : Path.GetFileName(previous.Folder)) + "; compare=" + diff.Count);
        }
        internal static void CheckSelfTestFixtures()
        {
            string root = Path.Combine(Path.GetTempPath(), "KeeneticObserverAnalyticsTest_" + Guid.NewGuid().ToString("N"));
            string current = Path.Combine(root, "TRAFFIC_Router_10_210_223_1_2026-11-24_19-00-00");
            Directory.CreateDirectory(current);
            try
            {
                TrafficResult result = new TrafficResult { Folder = current, Window = "24 часа" };
                result.Rows.Add(new TrafficRow { Rank = "1", Name = "TEST-PERSONAL", IP = "192.0.2.10", MAC = "aa:bb:cc:dd:ee:01", TotalBytes = 31457280,
                    DownloadBytes = 1048576, UploadBytes = 30408704, Download = "1.00 MiB", Upload = "29.00 MiB", Window = "24 часа" });
                result.Rows.Add(new TrafficRow { Rank = "2", Name = "=EXCEL-FORMULA", IP = "0.0.0.0", MAC = "aa:bb:cc:dd:ee:02", TotalBytes = 0,
                    DownloadBytes = 0, UploadBytes = 0, Download = "0 B", Upload = "0 B", Window = "24 часа" });
                Export(result, "192.0.2.1", DateTimeOffset.Now, delegate(string message) { });
                if (!File.Exists(result.Excel) || !File.Exists(Path.Combine(current, "09_Traffic_Comparison.csv")) ||
                    !File.Exists(Path.Combine(current, "10_Traffic_Flags.csv"))) throw new InvalidDataException("Traffic analytics files missing.");
                using (ZipArchive zip = ZipFile.OpenRead(result.Excel))
                {
                    ZipArchiveEntry all = zip.GetEntry("xl/worksheets/sheet2.xml");
                    ZipArchiveEntry chart = zip.GetEntry("xl/drawings/charts/chart1.xml");
                    if (all == null || chart == null) throw new InvalidDataException("XLSX traffic inventory/chart missing.");
                    using (Stream s = all.Open())
                    {
                        XDocument doc = XDocument.Load(s);
                        XElement value = doc.Descendants(Ns + "c").FirstOrDefault(x => (string)x.Attribute("r") == "B8");
                        if (value == null || !value.Value.Contains("TEST-PERSONAL")) throw new InvalidDataException("XLSX traffic inventory mismatch.");
                    }
                }
                string text = File.ReadAllText(Path.Combine(current, "10_Traffic_Flags.csv"));
                if (!text.Contains("HIGH_UPLOAD_SHARE") || !text.Contains("NO_CURRENT_IP")) throw new InvalidDataException("Traffic flags mismatch.");
            }
            finally { try { Directory.Delete(root, true); } catch (IOException) { } }
        }
    }
}
