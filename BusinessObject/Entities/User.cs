using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("Username", Name = "UQ_Users_Username", IsUnique = true)]
public partial class User
{
    [Key]
    public int UserId { get; set; }

    [StringLength(100)]
    public string Username { get; set; } = null!;

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string FullName { get; set; } = null!;

    [StringLength(20)]
    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<AIChatSession> AIChatSessions { get; set; } = new List<AIChatSession>();

    [InverseProperty("Sender")]
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    [InverseProperty("ActorUser")]
    public virtual ICollection<ChatModerationLog> ChatModerationLogActorUsers { get; set; } = new List<ChatModerationLog>();

    [InverseProperty("TargetUser")]
    public virtual ICollection<ChatModerationLog> ChatModerationLogTargetUsers { get; set; } = new List<ChatModerationLog>();

    [InverseProperty("User")]
    public virtual ICollection<ChatRoomMember> ChatRoomMembers { get; set; } = new List<ChatRoomMember>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();

    [InverseProperty("ResolvedByNavigation")]
    public virtual ICollection<GradeAppeal> GradeAppeals { get; set; } = new List<GradeAppeal>();

    [InverseProperty("ActorUser")]
    public virtual ICollection<GradeAuditLog> GradeAuditLogs { get; set; } = new List<GradeAuditLog>();

    [InverseProperty("RequestByNavigation")]
    public virtual ICollection<GradeBookApproval> GradeBookApprovalRequestByNavigations { get; set; } = new List<GradeBookApproval>();

    [InverseProperty("ResponseByNavigation")]
    public virtual ICollection<GradeBookApproval> GradeBookApprovalResponseByNavigations { get; set; } = new List<GradeBookApproval>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<GradeEntry> GradeEntries { get; set; } = new List<GradeEntry>();

    [InverseProperty("User")]
    public virtual ICollection<NotificationRecipient> NotificationRecipients { get; set; } = new List<NotificationRecipient>();

    [InverseProperty("ActorUser")]
    public virtual ICollection<ScheduleChangeLog> ScheduleChangeLogs { get; set; } = new List<ScheduleChangeLog>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<ScheduleEvent> ScheduleEventCreatedByNavigations { get; set; } = new List<ScheduleEvent>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<ScheduleEvent> ScheduleEventUpdatedByNavigations { get; set; } = new List<ScheduleEvent>();

    [InverseProperty("StudentNavigation")]
    public virtual Student? Student { get; set; }

    [InverseProperty("TeacherNavigation")]
    public virtual Teacher? Teacher { get; set; }
}
