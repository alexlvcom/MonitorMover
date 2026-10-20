namespace MonitorMover;

public enum UpdateCheckFrequency
{
    Daily = 0,
    Hourly = 1,
    Weekly = 2,
    Monthly = 3,
    Never = 4,
}

public static class UpdateSchedule
{
    public static UpdateCheckFrequency Normalize(UpdateCheckFrequency frequency) =>
        Enum.IsDefined(frequency) ? frequency : UpdateCheckFrequency.Daily;

    public static DateTime NextCheck(DateTime nowUtc, UpdateCheckFrequency frequency) => Normalize(frequency) switch
    {
        UpdateCheckFrequency.Hourly => nowUtc.AddHours(1),
        UpdateCheckFrequency.Weekly => nowUtc.AddDays(7),
        UpdateCheckFrequency.Monthly => nowUtc.AddMonths(1),
        UpdateCheckFrequency.Never => DateTime.MaxValue,
        _ => nowUtc.AddDays(1),
    };
}

/// <summary>Wording for update-check results, shared by the Settings status card and the Help menu message.</summary>
internal static class UpdateMessages
{
    /// <summary>"You're running the latest version (v1.3.0)"; never "no newer installer".</summary>
    internal static string Latest(string version) => $"You're running the latest version (v{version})";
}
