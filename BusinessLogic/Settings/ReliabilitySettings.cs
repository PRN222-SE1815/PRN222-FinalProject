namespace BusinessLogic.Settings;

public sealed class ReliabilitySettings
{
    public const string SectionName = "Reliability";

    public int NotificationRetryCount { get; set; } = 3;
    public int NotificationRetryBaseDelayMs { get; set; } = 200;
    public int SignalRFallbackPollingSeconds { get; set; } = 30;
    public int MaxScheduleQueryWindowDays { get; set; } = 120;
}
