namespace BusinessLogic.DTOs.Response;

public sealed class CreateUserResultDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}
