namespace BusinessLogic.DTOs.Request;

public class CreateTeacherDto : CreateUserDto
{
    public string TeacherCode { get; set; } = null!;
    public string? Department { get; set; }
}
