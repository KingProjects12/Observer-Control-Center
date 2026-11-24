namespace Observer.ControlCenter.Models;

public sealed class TrafficDevice
{
    public int Rank { get; init; }
    public string Name { get; init; } = "";
    public string IP { get; init; } = "";
    public string MAC { get; init; } = "";
    public string Download { get; init; } = "";
    public string Upload { get; init; } = "";
    public string Total { get; init; } = "";
    public string Status { get; init; } = "";
    public string Window { get; init; } = "";
    public long TotalBytes { get; init; }
}

public sealed class TopBar
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public double Width { get; init; }
}
