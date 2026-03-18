namespace BusinessLogic.Settings;

public sealed class AIChatSettings
{
    public const string SectionName = "AIChat";

    public int MaxHistoryMessages { get; set; } = 30;
    public int MaxUserMessageLength { get; set; } = 4000;
    public int MaxToolCallsPerTurn { get; set; } = 3;
    public int MaxMessagesPerMinutePerUser { get; set; } = 12;
}
