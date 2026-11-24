using System.Data;
using System.Globalization;
using System.Text;
using Observer.ControlCenter.Models;

namespace Observer.ControlCenter.Services;

/// <summary>Read-only parser. No network requests; handles delimiters, BOM and RFC-style quoted CSV cells.</summary>
public static class CsvTableReader
{
    public static DataTable ReadTable(string file)
    {
        if (new FileInfo(file).Length > 30_000_000)
            throw new InvalidDataException("Этот файл превышает лимит предпросмотра 30 МБ.");
        var contents = File.ReadAllText(file, Encoding.UTF8);
        char sep = DetectDelimiter(contents);
        var records = Parse(contents, sep);
        var table = new DataTable();
        if (records.Count == 0) return table;
        var headers = records[0];
        for (int i = 0; i < headers.Count; i++)
        {
            var name = headers[i].Trim().TrimStart('\uFEFF');
            if (name.Length == 0) name = "Column" + (i + 1);
            string unique = name;
            for (int suffix = 2; table.Columns.Contains(unique); suffix++) unique = name + "_" + suffix;
            table.Columns.Add(unique, typeof(string));
        }
        for (int i = 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.All(string.IsNullOrWhiteSpace)) continue;
            var row = table.NewRow();
            for (int j = 0; j < record.Count && j < table.Columns.Count; j++) row[j] = record[j];
            table.Rows.Add(row);
        }
        return table;
    }

    public static List<TrafficDevice> ReadTraffic(string file)
    {
        var table = ReadTable(file);
        string[] required = ["Rank", "Name", "IP", "MAC", "Download", "Upload", "Total", "TotalBytes"];
        if (required.Any(r => !table.Columns.Contains(r)))
            throw new InvalidDataException("Это не TOP TRAFFIC CSV: отсутствуют необходимые столбцы.");

        var devices = new List<TrafficDevice>();
        foreach (DataRow row in table.Rows)
        {
            string Get(string col) => table.Columns.Contains(col) ? Convert.ToString(row[col]) ?? "" : "";
            int.TryParse(Get("Rank"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rank);
            long.TryParse(Get("TotalBytes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count);
            devices.Add(new TrafficDevice
            {
                Rank = rank,
                Name = Get("Name"), IP = Get("IP"), MAC = Get("MAC"),
                Download = Get("Download"), Upload = Get("Upload"), Total = Get("Total"),
                TotalBytes = Math.Max(0, count), Status = Get("Status"), Window = Get("Window")
            });
        }
        return devices.OrderByDescending(x => x.TotalBytes).ToList();
    }

    private static char DetectDelimiter(string content)
    {
        string first = content.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return first.Count(c => c == ';') > first.Count(c => c == ',') ? ';' : ',';
    }

    public static List<List<string>> Parse(string input, char delimiter = ',')
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '"')
            {
                if (quoted && i + 1 < input.Length && input[i+1] == '"')
                {
                    field.Append('"'); i++;
                }
                else quoted = !quoted;
            }
            else if (!quoted && c == delimiter)
            {
                row.Add(field.ToString()); field.Clear();
            }
            else if (!quoted && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Any(v => v.Length > 0)) records.Add(row);
                row = [];
            }
            else field.Append(c);
        }
        if (quoted) throw new InvalidDataException("В CSV обнаружена незакрытая кавычка.");
        if (row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            if (row.Any(v => v.Length > 0)) records.Add(row);
        }
        return records;
    }
}
