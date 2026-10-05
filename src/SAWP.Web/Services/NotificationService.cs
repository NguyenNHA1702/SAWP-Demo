using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// In-system notification dispatcher (BR-42, BR-43, UC-45, UC-46).
/// </summary>
public interface INotificationService
{
    Task NotifyUserAsync(int userId, string title, string message, string? linkUrl = null, string type = "Workflow");
    Task NotifyRoleAsync(string roleName, string title, string message, string? linkUrl = null, string type = "Workflow");
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}

/// <summary>
/// Implementation of INotificationService.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly SawpDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(SawpDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task NotifyUserAsync(int userId, string title, string message, string? linkUrl = null, string type = "Workflow")
    {
        var notification = new Notification
        {
            RecipientUserId = userId,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Notification] Sent to User={UserId}: {Title}", userId, title);
    }

    public async Task NotifyRoleAsync(string roleName, string title, string message, string? linkUrl = null, string type = "Workflow")
    {
        var users = await _db.UserRoles
            .Where(ur => ur.Role.Name == roleName && ur.User.Status == UserStatus.Active)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in users)
        {
            _db.Notifications.Add(new Notification
            {
                RecipientUserId = userId,
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Notification] Broadcasted to Role={Role} (Recipients={Count}): {Title}", roleName, users.Count, title);
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _db.Notifications
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId);

        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}
