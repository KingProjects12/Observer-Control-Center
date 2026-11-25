// Keenetic Observer v1.8 - optional import of existing authorized CENTRAL flow logs.
// This is OFFLINE file processing only. No network traffic, router configuration or scanning.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace KeeneticObserver
{
    internal static class CentralFlowImporter
    {
        // Never assume that an imported row actually originated on a Keenetic router.
        // The operator supplies a pre-existing export of central firewall/VPN/flow logs.
        internal static CollectionResult Import(string sourceFile, Action<string> report, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                throw new FileNotFoundException("Выберите существующий CSV-файл с центрального шлюза.");
            if (new FileInfo(sourceFile).Length > 100L * 1024L * 1024L)
                throw new InvalidDataException("Один импорт ограничен 100 МБ. Разделите существующий экспорт на части.");
            CollectionResult result = ParseFile(sourceFile, token);
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KeeneticObserver", "Results");
            string folder = Path.Combine(root, "CENTRAL_FLOW_IMPORT_" +
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_" +
                Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(folder);
            result.Folder = folder;
            result.Status = "IMPORTED (EXTERNAL LOG)";
            result.Imported = true;
            result.ImportedFile = Path.GetFileName(sourceFile);
            string evidence = Path.Combine(folder, "Imported_Evidence");
            Directory.CreateDirectory(evidence);
            File.Copy(sourceFile, Path.Combine(evidence, "Original_" + Path.GetFileName(sourceFile)));
            File.WriteAllText(Path.Combine(folder, "DIAGNOSTICS.log"),
                "SOURCE=CENTRAL_FLOW_CSV; original filename=" + result.ImportedFile + Environment.NewLine +
                "No Keenetic API called; offline import of existing authorized data." + Environment.NewLine +
                "Rows imported=" + result.All.Count + "; skipped=" + result.SkippedRecords +
                "; TO OFFICE=" + result.Target.Count + Environment.NewLine +
                "The actual CSV provenance and time basis must be verified by the operator." + Environment.NewLine,
                new UTF8Encoding(true));
            token.ThrowIfCancellationRequested();
            Engine.SaveReport(result, CancellationToken.None);
            report("CENTRAL IMPORT // импортировано " + result.All.Count + " записей; TO_OFFICE=" + result.Target.Count +
                "; skipped=" + result.SkippedRecords + "; источник=внешний CSV; роутеры не опрашивались.");
            report("REPORT // " + result.Excel);
            return result;
        }

        // This method has no filesystem writes and is used by both import and deterministic tests.
        internal static CollectionResult ParseFile(string file, CancellationToken token)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("CSV не найден.", file);
            var result = new CollectionResult { Imported = true, ImportedFile = Path.GetFileName(file),
                RouteCheck = "N/A (CSV import)", InterfaceCheck = "N/A (CSV import)" };
            using (var reader = new StreamReader(file, Encoding.UTF8, true))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine)) throw new InvalidDataException("CSV не содержит заголовков.");
                char delimiter = ChooseDelimiter(headerLine);
                List<string> headers = ParseLine(headerLine, delimiter);
                int sourceIndex = Column(headers, "sourceip", "srcip", "src", "sourceaddress", "sourceipv4");
                int destinationIndex = Column(headers, "destinationip", "dstip", "dst", "destinationaddress", "destinationipv4");
                int protocolIndex = Column(headers, "protocol", "proto", "ipprotocol", "transportprotocol");
                if (sourceIndex < 0 || destinationIndex < 0 || protocolIndex < 0)
                    throw new InvalidDataException("CSV должен содержать SourceIP, DestinationIP и Protocol. Образец: CentralFlow_Import_Template.csv");
                int sportIndex = Column(headers, "sourceport", "srcport", "sport");
                int dportIndex = Column(headers, "destinationport", "dstport", "dport");
                int timeIndex = Column(headers, "timestamp", "time", "starttime", "firstseen", "datetime");
                int nameIndex = Column(headers, "sourcename", "devicename", "hostname", "clientname");
                int noteIndex = Column(headers, "service", "servicename", "application", "note");
                int siteIndex = Column(headers, "site", "sitename", "branch", "location");
                int rowNumber = 0;
                while (!reader.EndOfStream)
                {
                    if ((++rowNumber % 200) == 0) token.ThrowIfCancellationRequested();
                    if (rowNumber > 300000) throw new InvalidDataException("CSV содержит более 300000 строк; разделите экспорт на части.");
                    List<string> row = ReadRow(reader, delimiter);
                    if (row == null) break;
                    if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
                    string src = Val(row, sourceIndex).Trim(), dst = Val(row, destinationIndex).Trim();
                    IPAddress srcIP, dstIP;
                    if (!IPAddress.TryParse(src, out srcIP) || !IPAddress.TryParse(dst, out dstIP))
                    { result.SkippedRecords++; continue; }
                    string sport = Val(row, sportIndex).Trim(), dport = Val(row, dportIndex).Trim();
                    int port;
                    if ((!string.IsNullOrEmpty(sport) && (!int.TryParse(sport, out port) || port < 0 || port > 65535)) ||
                        (!string.IsNullOrEmpty(dport) && (!int.TryParse(dport, out port) || port < 0 || port > 65535)))
                    { result.SkippedRecords++; continue; }
                    string protocol = Val(row, protocolIndex).Trim().ToUpperInvariant();
                    if (protocol.Length == 0) { result.SkippedRecords++; continue; }
                    // Standard IANA protocol numbers, if the exported log uses numbers.
                    if (protocol == "6") protocol = "TCP";
                    else if (protocol == "17") protocol = "UDP";
                    else if (protocol == "1") protocol = "ICMP";
                    else if (protocol == "47") protocol = "GRE";
                    else if (protocol == "50") protocol = "ESP";
                    string textFromSource = Val(row, noteIndex).Trim();
                    string site = Val(row, siteIndex).Trim();
                    var entry = new Entry {
                        Timestamp = Val(row, timeIndex).Trim(), Sample = "IMPORT", Router = site,
                        SourceName = Val(row, nameIndex).Trim(), SourceIP = src, SourcePort = sport,
                        DestinationIP = dst, DestinationPort = dport, Protocol = protocol,
                        TargetMatch = Engine.TargetIP(dst) ? "YES" : "NO", EvidenceSource = "CENTRAL_FLOW_CSV",
                        Note = textFromSource.Length == 0 ? "EXTERNAL_CSV_UNVERIFIED" :
                            "EXTERNAL_CSV_UNVERIFIED / Service as reported: " + textFromSource
                    };
                    entry.OfficeFields = Engine.OfficeEvidenceFields(entry);
                    entry.OfficeDirection = Engine.OfficeDirectionFor(entry);
                    result.All.Add(entry);
                    if (entry.TargetMatch == "YES") result.Target.Add(entry);
                    if (entry.OfficeFields.Length > 0) result.OfficeRelated.Add(entry);
                }
            }
            if (result.All.Count == 0)
                throw new InvalidDataException("Не найдено корректных соединений. Проверьте столбцы и разделитель CSV.");
            Engine.Summarize(result);
            return result;
        }

        private static char ChooseDelimiter(string header)
        {
            int commas = 0, semicolons = 0; bool quoted = false;
            for (int i = 0; i < header.Length; i++)
            {
                if (header[i] == '"') quoted = !quoted;
                else if (!quoted && header[i] == ',') commas++;
                else if (!quoted && header[i] == ';') semicolons++;
            }
            if (commas == 0 && semicolons == 0) throw new InvalidDataException("Нужен CSV с запятыми или точками с запятой.");
            return semicolons > commas ? ';' : ',';
        }
        private static int Column(List<string> headers, params string[] names)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                string normalized = new string(headers[i].ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                if (names.Contains(normalized)) return i;
            }
            return -1;
        }
        private static string Val(List<string> row, int index)
        { return index >= 0 && index < row.Count ? row[index] : ""; }
        private static List<string> ParseLine(string line, char separator)
        {
            using (var reader = new StringReader(line)) return ReadRow(reader, separator);
        }
        private static List<string> ReadRow(TextReader reader, char separator)
        {
            var columns = new List<string>();
            var current = new StringBuilder();
            bool quoted = false, any = false;
            int ci;
            while ((ci = reader.Read()) != -1)
            {
                any = true;
                char c = (char)ci;
                if (c == '"')
                {
                    if (quoted && reader.Peek() == '"') { reader.Read(); current.Append('"'); }
                    else quoted = !quoted;
                }
                else if (!quoted && c == separator)
                { columns.Add(current.ToString()); current.Length = 0; }
                else if (!quoted && (c == '\n' || c == '\r'))
                {
                    if (c == '\r' && reader.Peek() == '\n') reader.Read();
                    break;
                }
                else current.Append(c);
            }
            if (!any) return null;
            if (quoted) throw new InvalidDataException("Некорректные кавычки в CSV.");
            columns.Add(current.ToString());
            return columns;
        }

        internal static void CheckSelfTestFixtures()
        {
            string path = Path.Combine(Path.GetTempPath(), "observer_import_fixture_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(path, "SourceIP,DestinationIP,Protocol,DestinationPort,SourceName,Timestamp,Service\r\n" +
                    "192.0.2.101,203.0.113.34,TCP,443,CAS01,2026-11-24T10:00:00+03:00,Known from exporter\r\n" +
                    "192.0.2.102,198.51.100.4,17,53,ST02,2026-11-24T10:01:00+03:00,\r\n" +
                    "203.0.113.88,192.0.2.101,1,,SERVER,,\r\n" +
                    "invalid,203.0.113.25,TCP,80,BAD,,\r\n", new UTF8Encoding(true));
                CollectionResult parsed = ParseFile(path, CancellationToken.None);
                if (parsed.All.Count != 3 || parsed.Target.Count != 1 || parsed.OfficeRelated.Count != 2 ||
                    parsed.SkippedRecords != 1 || parsed.All[1].Protocol != "UDP" ||
                    parsed.All[2].OfficeDirection != "FROM_OFFICE" || parsed.Routes.Count != 1)
                    throw new InvalidDataException("Central flow import fixture mismatch.");
            }
            finally { try { File.Delete(path); } catch (IOException) { } }
        }
    }
}
