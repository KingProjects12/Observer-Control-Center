using System.Text.Json;
using Observer.ControlCenter.Services;

int passed = 0;
void Assert(bool good, string label)
{
    if (!good) throw new Exception("FAILED: " + label);
    Console.WriteLine("PASS: " + label);
    passed++;
}
ReleaseVersion V(string s)
{
    Assert(ReleaseVersion.TryParse(s, out var version), "parse " + s);
    return version;
}
// Regression: the empty prerelease string must be allowed for stable releases.
Assert(ReleaseVersion.TryParse("v2.0.1", out var released) && released.Prerelease == "", "stable release parses");
Assert(ReleaseVersion.TryParse("2.0.2", out _), "stable release without v parses");
Assert(ReleaseVersion.TryParse("v2.0.3+build.9", out _), "build metadata parses");
Assert(!ReleaseVersion.TryParse("2.0.3-rc.01", out _), "prerelease numeric leading zero rejected");
var stable = V("v2.0.1");
var preview = V("2.0.1-preview.1");
Assert(preview.CompareTo(stable) < 0, "stable beats preview");
Assert(V("2.1.0").CompareTo(stable) > 0, "minor version upgrade");
Assert(V("2.0.1-preview.2").CompareTo(preview) > 0, "preview number ordering");
Assert(V("2.0.1-alpha.10").CompareTo(V("2.0.1-alpha.2")) > 0, "numeric prerelease ordering");
Assert(V("2.0.1-alpha.9999999999999999999999999999999").CompareTo(V("2.0.1-alpha.10000000000000000000000000000000")) < 0, "huge SemVer numeric identifiers");
Assert(V("2.0.1-beta").CompareTo(V("2.0.1-alpha")) > 0, "lexicographic prerelease ordering");
Assert(!ReleaseVersion.TryParse("v2.0.1-preview.01", out _), "invalid numeric identifier rejected");
Assert(!ReleaseVersion.TryParse("/2.0.1", out _), "invalid tag rejected");
Assert(GitHubReleaseClient.IsValidRepository("example/observer"), "valid repository");
Assert(!GitHubReleaseClient.IsValidRepository("..//evil"), "malformed repository rejected");
Assert(!GitHubReleaseClient.IsValidRepository("owner/../evil"), "path traversal rejected");
Assert(!GitHubReleaseClient.IsValidRepository("https://github.com/owner/repo"), "URL not accepted as repo slug");
string Json(string tag, bool draft = false, bool prerelease = false) =>
    JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease, body = "Fixed report loading", published_at = "2026-11-25T00:00:00Z", html_url = "https://attacker.invalid/false" });
var update = GitHubReleaseClient.ParseLatest(Json("v2.1.0"), "example/observer", V("2.0.1-preview.1"));
Assert(update != null && update.Version.CompareTo(V("2.1.0")) == 0, "new stable release found");
Assert(update?.ReleaseUrl == "https://github.com/example/observer/releases/latest", "release URL derived from approved repository");
Assert(update?.Notes == "Fixed report loading", "release notes retained");
Assert(GitHubReleaseClient.ParseLatest(Json("v2.0.0"), "example/observer", V("2.0.1-preview.1")) == null, "old stable not offered");
Assert(GitHubReleaseClient.ParseLatest(Json("v2.1.0", prerelease: true), "example/observer", stable) == null, "prerelease excluded");
Assert(GitHubReleaseClient.ParseLatest(Json("v2.1.0", draft: true), "example/observer", stable) == null, "draft excluded");
Assert(UpdateSettingsStore.ResolveRepository("attacker/repo", "owner/observer") == "owner/observer", "embedded repository wins");
Assert(UpdateSettingsStore.ResolveRepository("owner/custom", "") == "owner/custom", "local preview may select repository");
Assert(UpdateSettingsStore.ResolveRepository("invalid/slash/slug", "") == "", "invalid repository ignored");
Assert(UpdatePollingPolicy.TestInterval == TimeSpan.FromMinutes(1), "testing interval is exactly one minute");
Assert(UpdatePollingPolicy.RateLimitCooldown == TimeSpan.FromMinutes(30), "rate limit cooldown is thirty minutes");
var blockedAt = DateTimeOffset.Parse("2026-11-25T00:00:00Z");
Assert(UpdatePollingPolicy.IsThrottled(blockedAt.AddMinutes(1), blockedAt.AddMinutes(30)), "rate limited checks are skipped");
Assert(!UpdatePollingPolicy.IsThrottled(blockedAt.AddMinutes(30), blockedAt.AddMinutes(30)), "checks resume when cooldown ends");
Console.WriteLine($"ALL {passed} UPDATE CHECKS PASSED");
