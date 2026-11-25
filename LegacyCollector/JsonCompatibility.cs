// Compatibility layer for the original .NET Framework collector. No external dependencies.
using System.Text.Json;
namespace System.Web.Script.Serialization;
internal sealed class JavaScriptSerializer
{
    public int MaxJsonLength { get; set; } = 50_000_000;
    public int RecursionLimit { get; set; } = 100;
    public string Serialize(object value) => JsonSerializer.Serialize(value);
    public object? DeserializeObject(string json)
    {
        if (json.Length > MaxJsonLength) throw new ArgumentException("JSON exceeds size limit.");
        try { using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = RecursionLimit }); return ConvertNode(doc.RootElement); }
        catch (JsonException ex) { throw new ArgumentException("Invalid JSON", ex); }
    }
    private static object? ConvertNode(JsonElement node) => node.ValueKind switch
    {
        JsonValueKind.Object => Object(node),
        JsonValueKind.Array => node.EnumerateArray().Select(ConvertNode).ToArray(),
        JsonValueKind.String => node.GetString(),
        JsonValueKind.Number => node.TryGetInt64(out long n) ? (object)n : node.TryGetDecimal(out decimal d) ? (object)d : node.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, _ => null
    };
    private static Dictionary<string, object?> Object(JsonElement node)
    {
        var result = new Dictionary<string, object?>();
        foreach (var p in node.EnumerateObject()) result[p.Name] = ConvertNode(p.Value);
        return result;
    }
}
