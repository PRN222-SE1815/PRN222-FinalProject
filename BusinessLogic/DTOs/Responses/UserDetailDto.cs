using System;

namespace BusinessLogic.DTOs.Response;

public sealed class UserDetailDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? StudentCode { get; set; }
    public int? ProgramId { get; set; }
    public int? CurrentSemesterId { get; set; }
    public string? TeacherCode { get; set; }
    public string? Department { get; set; }
}
