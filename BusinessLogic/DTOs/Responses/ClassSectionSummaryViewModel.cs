namespace BusinessLogic.DTOs.Response;

public sealed class ClassSectionSummaryViewModel
{
    public int ClassSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string SemesterCode { get; set; } = string.Empty;
    public int SemesterId { get; set; }
    public string TeacherFullName { get; set; } = string.Empty;
    public int CurrentEnrollment { get; set; }
    public int MaxCapacity { get; set; }
    public bool IsOpen { get; set; }
    public decimal EstimatedFee { get; set; }
    public bool CanRegister { get; set; }
}
