namespace Observer.ControlCenter.Services;

/// <summary>
/// Automatic polling and the separate rate-limit cooldown.
/// </summary>
public static class UpdatePollingPolicy
{
    public static readonly TimeSpan AutomaticInterval = TimeSpan.FromHours(6);
    public static readonly TimeSpan RateLimitCooldown = TimeSpan.FromMinutes(30);

    public static bool IsThrottled(DateTimeOffset now, DateTimeOffset resumeAtUtc) =>
        now < resumeAtUtc;
}
