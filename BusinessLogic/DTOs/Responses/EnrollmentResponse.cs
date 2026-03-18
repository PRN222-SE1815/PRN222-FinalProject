namespace BusinessLogic.DTOs.Response;

public sealed class EnrollmentResponse
{
    public int EnrollmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ClassSectionId { get; set; }
    public int CourseId { get; set; }
    public int SemesterId { get; set; }
    public int Credits { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal WalletBalance { get; set; }
    public string? Message { get; set; }
    public IReadOnlyList<PrerequisiteInfoDto>? MissingPrerequisites { get; set; }
}
