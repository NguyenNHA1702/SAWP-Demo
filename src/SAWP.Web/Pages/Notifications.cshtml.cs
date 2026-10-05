using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages;

public class NotificationsModel : AppPageModel
{
    private readonly INotificationService _notificationService;

    public NotificationsModel(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public List<Notification> NotificationsList { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CurrentUser.UserId.HasValue) return RedirectToPage("/Account/Login");

        NotificationsList = await Db.Notifications
            .Where(n => n.RecipientUserId == CurrentUser.UserId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync()
    {
        if (!CurrentUser.UserId.HasValue) return RedirectToPage("/Account/Login");

        await _notificationService.MarkAllAsReadAsync(CurrentUser.UserId.Value);
        Toast("MSG37");

        return RedirectToPage("/Notifications");
    }

    public async Task<IActionResult> OnPostMarkReadAsync(int id)
    {
        if (!CurrentUser.UserId.HasValue) return RedirectToPage("/Account/Login");

        await _notificationService.MarkAsReadAsync(id, CurrentUser.UserId.Value);
        Toast("MSG37");

        return RedirectToPage("/Notifications");
    }
}
