namespace BusinessLogic.DTOs.Response;

public sealed class RegistrationSummaryDto
{
    public int? SelectedSemesterId { get; set; }
    public string? SelectedSemesterCode { get; set; }
    public int? CurrentSemesterId { get; set; }
    public bool IsPastSemester { get; set; }
    public bool IsRegistrationClosed { get; set; }
    public DateOnly? RegistrationEndDate { get; set; }
    public DateOnly? AddDropDeadline { get; set; }
    public IReadOnlyList<SemesterOptionDto> Semesters { get; set; } = [];
    public IReadOnlyList<ClassSectionSummaryViewModel> ClassSections { get; set; } = [];
    public decimal? WalletBalance { get; set; }
}
