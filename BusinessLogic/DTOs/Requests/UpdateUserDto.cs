namespace BusinessLogic.DTOs.Request;

public sealed class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public string? Password { get; set; }
    public string? StudentCode { get; set; }
    public string? TeacherCode { get; set; }
    public string? Department { get; set; }
}
