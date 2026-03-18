namespace BusinessLogic.DTOs.Request;

public class CreateStudentDto : CreateUserDto
{
    public string StudentCode { get; set; } = null!;
    public int? ProgramId { get; set; }
    public int? CurrentSemesterId { get; set; }
}
