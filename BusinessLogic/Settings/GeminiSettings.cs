namespace BusinessLogic.Settings;

public sealed class GeminiSettings
{
    public const string SectionName = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 2048;
}
