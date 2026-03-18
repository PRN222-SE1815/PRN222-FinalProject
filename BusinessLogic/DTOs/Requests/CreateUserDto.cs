namespace BusinessLogic.DTOs.Request;

public class CreateUserDto
{
    public string Username { get; set; } = null!;
    public string? Password { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
}
