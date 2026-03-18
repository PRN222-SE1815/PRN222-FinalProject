namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class GradeBookExportEntryRaw
{
    public int EnrollmentId { get; set; }

    public int GradeItemId { get; set; }

    public decimal? Score { get; set; }
}
