namespace BusinessLogic.DTOs.Response;

public sealed class AvailableUserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? Email { get; set; }
}
