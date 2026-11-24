using System.Globalization;
using System.Text.RegularExpressions;

namespace Observer.ControlCenter.Services;

/// <summary>SemVer 2.0 precedence, including preview / beta tags (without external packages).</summary>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch, string Prerelease) : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(
        @"^[vV]?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(string? input, out ReleaseVersion value)
    {
        value = default;
        var match = Pattern.Match((input ?? "").Trim());
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minor) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int patch)) return false;
        var pre = match.Groups[4].Success ? match.Groups[4].Value : "";
        if (pre.Length > 0 && pre.Split('.').Any(x => x.Length == 0 || (x.Length > 1 && x[0] == '0' && x.All(char.IsDigit)))) return false;
        value = new(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        int core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        if (Prerelease.Length == 0) return other.Prerelease.Length == 0 ? 0 : 1;
        if (other.Prerelease.Length == 0) return -1;
        var left = Prerelease.Split('.'); var right = other.Prerelease.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            // SemVer numeric identifiers can exceed Int64: compare length, then ordinal digits.
            bool aNumber = left[i].All(char.IsDigit);
            bool bNumber = right[i].All(char.IsDigit);
            int difference = aNumber && bNumber
                ? (left[i].Length != right[i].Length
                    ? left[i].Length.CompareTo(right[i].Length)
                    : string.Compare(left[i], right[i], StringComparison.Ordinal))
                : aNumber ? -1 : bNumber ? 1
                : string.Compare(left[i], right[i], StringComparison.Ordinal);
            if (difference != 0) return difference;
        }
        return left.Length.CompareTo(right.Length);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Prerelease.Length > 0 ? "-" + Prerelease : "");
}
