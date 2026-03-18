namespace BusinessLogic.DTOs.Request;

public sealed class UserFilterDto
{
    public string? Role { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
