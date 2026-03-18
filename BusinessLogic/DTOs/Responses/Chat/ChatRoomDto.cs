namespace BusinessLogic.DTOs.Responses.Chat;

public class ChatRoomDto
{
    public int RoomId { get; set; }
    public string RoomType { get; set; } = null!;
    public int? CourseId { get; set; }
    public int? ClassSectionId { get; set; }
    public string RoomName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CurrentUserRole { get; set; }
    public string? CurrentMemberStatus { get; set; }
}
