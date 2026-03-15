using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class Notification
{
    [Key]
    public long NotificationId { get; set; }

    [StringLength(50)]
    public string NotificationType { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? SentAt { get; set; }

    [InverseProperty("Notification")]
    public virtual ICollection<NotificationRecipient> NotificationRecipients { get; set; } = new List<NotificationRecipient>();
}
