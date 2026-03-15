using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class ChatRoom
{
    [Key]
    public int RoomId { get; set; }

    [StringLength(20)]
    public string RoomType { get; set; } = null!;

    public int? CourseId { get; set; }

    public int? ClassSectionId { get; set; }

    [StringLength(200)]
    public string RoomName { get; set; } = null!;

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Room")]
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    [InverseProperty("Room")]
    public virtual ICollection<ChatModerationLog> ChatModerationLogs { get; set; } = new List<ChatModerationLog>();

    [InverseProperty("Room")]
    public virtual ICollection<ChatRoomMember> ChatRoomMembers { get; set; } = new List<ChatRoomMember>();

    [ForeignKey("ClassSectionId")]
    [InverseProperty("ChatRooms")]
    public virtual ClassSection? ClassSection { get; set; }

    [ForeignKey("CourseId")]
    [InverseProperty("ChatRooms")]
    public virtual Course? Course { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("ChatRooms")]
    public virtual User CreatedByNavigation { get; set; } = null!;
}
