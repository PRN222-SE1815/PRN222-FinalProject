namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class GradeExportRowDto
{
    public int StudentId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string GradeBookStatus { get; set; } = string.Empty;

    public Dictionary<string, decimal?> ItemScores { get; set; } = [];

    public decimal Total { get; set; }
}
