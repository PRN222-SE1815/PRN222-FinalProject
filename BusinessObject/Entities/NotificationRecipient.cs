using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[PrimaryKey("NotificationId", "UserId")]
public partial class NotificationRecipient
{
    [Key]
    public long NotificationId { get; set; }

    [Key]
    public int UserId { get; set; }

    [Precision(0)]
    public DateTime? DeliveredAt { get; set; }

    [Precision(0)]
    public DateTime? ReadAt { get; set; }

    [ForeignKey("NotificationId")]
    [InverseProperty("NotificationRecipients")]
    public virtual Notification Notification { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("NotificationRecipients")]
    public virtual User User { get; set; } = null!;
}
