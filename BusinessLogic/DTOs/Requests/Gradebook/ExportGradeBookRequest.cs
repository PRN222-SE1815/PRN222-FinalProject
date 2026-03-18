namespace BusinessLogic.DTOs.Requests.Gradebook;

public sealed class ExportGradeBookRequest
{
    public int ClassSectionId { get; set; }

    public string Format { get; set; } = string.Empty;
}
