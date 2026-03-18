namespace BusinessLogic.DTOs.Response;

public sealed class SemesterOptionDto
{
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
