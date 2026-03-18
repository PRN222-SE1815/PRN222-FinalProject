using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly SchoolManagementDbContext _context;

    public NotificationRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> CreateNotificationAsync(
        Notification notification,
        IReadOnlyCollection<int> recipientUserIds,
        CancellationToken ct = default)
    {
        var recipients = recipientUserIds
            .Where(id => id > 0)
            .Distinct()
            .Select(userId => new NotificationRecipient
            {
                UserId = userId,
                DeliveredAt = null,
                ReadAt = null
            })
            .ToList();

        foreach (var recipient in recipients)
        {
            notification.NotificationRecipients.Add(recipient);
        }

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        return notification;
    }

    public async Task<(IReadOnlyList<NotificationRecipient> Items, int TotalCount)> GetUserNotificationsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 20;
        }

        var query = _context.NotificationRecipients
            .AsNoTracking()
            .Include(x => x.Notification)
            .Where(x => x.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.Notification.CreatedAt)
            .ThenByDescending(x => x.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
    {
        return _context.NotificationRecipients
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.ReadAt.HasValue, ct);
    }

    public Task<bool> ExistsForUserAsync(long notificationId, int userId, CancellationToken ct = default)
    {
        return _context.NotificationRecipients
            .AsNoTracking()
            .AnyAsync(x => x.NotificationId == notificationId && x.UserId == userId, ct);
    }

    public async Task MarkAsReadAsync(long notificationId, int userId, CancellationToken ct = default)
    {
        var recipient = await _context.NotificationRecipients
            .SingleOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId, ct);

        if (recipient is null || recipient.ReadAt.HasValue)
        {
            return;
        }

        recipient.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        var unreadItems = await _context.NotificationRecipients
            .Where(x => x.UserId == userId && !x.ReadAt.HasValue)
            .ToListAsync(ct);

        if (unreadItems.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var recipient in unreadItems)
        {
            recipient.ReadAt = now;
        }

        await _context.SaveChangesAsync(ct);
    }
}
