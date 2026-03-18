namespace BusinessLogic.Settings;

public sealed class ExportSettings
{
    public const string SectionName = "Export";

    public int MaxExportRows { get; set; } = 5000;

    public int ExportTimeoutSeconds { get; set; } = 30;
}
