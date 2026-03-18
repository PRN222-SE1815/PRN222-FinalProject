namespace BusinessLogic.DTOs.Response;

public class AuthUserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
}
