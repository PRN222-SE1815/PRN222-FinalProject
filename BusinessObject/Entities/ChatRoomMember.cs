using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[PrimaryKey("RoomId", "UserId")]
public partial class ChatRoomMember
{
    [Key]
    public int RoomId { get; set; }

    [Key]
    public int UserId { get; set; }

    [StringLength(20)]
    public string RoleInRoom { get; set; } = null!;

    [StringLength(20)]
    public string MemberStatus { get; set; } = null!;

    public long? LastReadMessageId { get; set; }

    [Precision(0)]
    public DateTime JoinedAt { get; set; }

    [ForeignKey("RoomId")]
    [InverseProperty("ChatRoomMembers")]
    public virtual ChatRoom Room { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ChatRoomMembers")]
    public virtual User User { get; set; } = null!;
}
