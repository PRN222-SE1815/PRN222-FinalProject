namespace BusinessLogic.DTOs.Requests;

public class CreateTeacherDto : CreateUserDto
{
    public string TeacherCode { get; set; } = null!;
    public string? Department { get; set; }
}
