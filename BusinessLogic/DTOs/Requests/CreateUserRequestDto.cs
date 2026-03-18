namespace BusinessLogic.DTOs.Request;

public sealed class CreateUserRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public int? ProgramId { get; set; }
    public int? CurrentSemesterId { get; set; }
    public string? TeacherCode { get; set; }
    public string? Department { get; set; }
}
