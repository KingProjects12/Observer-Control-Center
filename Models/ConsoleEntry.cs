using System.Windows.Media;

namespace Observer.ControlCenter.Models;

/// <summary>A display-only log line. Classification never changes the underlying message.</summary>
public sealed class ConsoleEntry
{
    private static readonly Brush Good = NewBrush("#70E6AF");
    private static readonly Brush Bad = NewBrush("#FF768B");
    private static readonly Brush Warn = NewBrush("#FFD27E");
    private static readonly Brush SystemInfo = NewBrush("#7ABFFF");
    private static readonly Brush Normal = NewBrush("#B3C3D3");

    private static Brush NewBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    public string Text { get; }
    public Brush Color { get; }
    public string Category { get; }

    public ConsoleEntry(string text, string category)
    {
        Text = text;
        Category = category;
        Color = category switch
        {
            "SUCCESS" => Good,
            "ERROR" => Bad,
            "WARNING" => Warn,
            "SYSTEM" => SystemInfo,
            _ => Normal
        };
    }

    public static string Classify(string message)
    {
        string line = message.ToUpperInvariant();
        if (line.Contains("ERROR") || line.Contains("FAILED") || line.Contains("REFUSED") || line.Contains("TIMEOUT") || line.Contains("UNREACHABLE") ||
            line.Contains("EXCEPTION") || line.Contains("ОШИБК") ||
            line.Contains("ОТКАЗ") || line.Contains("DENIED"))
            return "ERROR";
        if (line.Contains("WARNING") || line.Contains("WARN") ||
            line.Contains("ВНИМАН") || line.Contains("PARTIAL") || line.Contains("UDP OBSERVED") || line.Contains("NOT OBSERVED LATER") || line.Contains("NO MATCHES") || line.Contains("REVIEW"))
            return "WARNING";
        if (line.Contains("CANCELLED") || line.Contains("NOT_STARTED") || line.Contains("NOT CHECKED") || line.Contains("SKIPPED")) return "INFO";
        if (line.Contains("COMPLETE") || line.Contains("TCP OPEN") || line.Contains("OBSERVED BOTH") || line.Contains("VERIFIED") || line.Contains("СОВПАДАЕТ") || line.Contains("SUCCESS") || line.Contains("PASS") || line.Contains("READY") ||
            line.Contains("CONNECTED") || line.Contains("IMPORTED") ||
            line.Contains("CSV IMPORT") || line.Contains("ГOTOB") ||
            line.Contains("ГОТОВО") || line.Contains("ЗАГРУЖЕН") ||
            line.Contains("DONE"))
            return "SUCCESS";
        if (line.Contains("SYSTEM") || line.Contains("INSTALL") ||
            line.Contains("DOWNLOAD") || line.Contains("UPDATE") ||
            line.Contains("NETWORK") || line.Contains("HEALTH") ||
            line.Contains("CHECK") || line.Contains("START") ||
            line.Contains("СИСТЕМ") || line.Contains("ПРОВЕР") ||
            line.Contains("ЗАГРУЗК"))
            return "SYSTEM";
        return "INFO";
    }
}
