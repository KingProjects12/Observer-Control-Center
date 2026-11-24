namespace Observer.ControlCenter.Services;

/// <summary>
/// Temporary rapid polling while we validate GitHub Releases notifications.
/// Return to six-hour checks before the production release.
/// </summary>
public static class UpdatePollingPolicy
{
    public static readonly TimeSpan TestInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RateLimitCooldown = TimeSpan.FromMinutes(30);

    public static bool IsThrottled(DateTimeOffset now, DateTimeOffset resumeAtUtc) =>
        now < resumeAtUtc;
}
