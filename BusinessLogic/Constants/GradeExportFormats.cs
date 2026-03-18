namespace BusinessLogic.Constants;

public static class GradeExportFormats
{
    public const string Csv = "CSV";
    public const string Xlsx = "XLSX";

    public static readonly IReadOnlySet<string> SupportedFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Csv,
            Xlsx
        };
}
