namespace Observer.ControlCenter.Services;

public static class ReportLocator
{
    public static readonly string ResultsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeeneticObserver", "Results");

    public static string? FindLatest(string glob)
    {
        if (!Directory.Exists(ResultsRoot)) return null;
        try
        {
            return Directory.EnumerateFiles(ResultsRoot, glob, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
