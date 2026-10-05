using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Board.Announcements;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public IndexModel(
        IAuditService auditService,
        INotificationService notificationService)
    {
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public List<Announcement> Announcements { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Announcements = await Db.Announcements
            .Include(a => a.PublishedByUser)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int? id, string title, string content, AnnouncementAudience targetAudience, bool isPinned, DateTime? expireAt)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            Toast("MSG01", "Tiêu đề và nội dung thông báo không được để trống.", "error");
            return RedirectToPage();
        }

        Announcement? item;
        bool isNew = false;

        if (id.HasValue && id.Value > 0)
        {
            item = await Db.Announcements.FindAsync(id.Value);
            if (item == null)
            {
                Toast("MSG09");
                return RedirectToPage();
            }

            item.Title = title.Trim();
            item.Content = content.Trim();
            item.TargetAudience = targetAudience;
            item.IsPinned = isPinned;
            item.ExpireAt = expireAt;
        }
        else
        {
            isNew = true;
            item = new Announcement
            {
                Title = title.Trim(),
                Content = content.Trim(),
                TargetAudience = targetAudience,
                PublishedByUserId = CurrentUser.UserId ?? 1,
                PublishedAt = DateTime.UtcNow,
                ExpireAt = expireAt,
                IsPinned = isPinned,
                IsActive = true
            };
            Db.Announcements.Add(item);
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            isNew ? "CreateAnnouncement" : "UpdateAnnouncement",
            nameof(Announcement),
            item.Id.ToString(),
            $"BGH {(isNew ? "đăng" : "cập nhật")} thông báo '{item.Title}' cho đối tượng: {item.TargetAudience}");

        if (isNew)
        {
            // Send in-system notification to target audience (BR-42)
            string roleName = targetAudience switch
            {
                AnnouncementAudience.Parents => nameof(SystemRole.Parent),
                AnnouncementAudience.Teachers => nameof(SystemRole.Teacher),
                AnnouncementAudience.Staff => nameof(SystemRole.Office),
                _ => ""
            };

            if (!string.IsNullOrEmpty(roleName))
            {
                await _notificationService.NotifyRoleAsync(
                    roleName,
                    $"Thông báo mới từ BGH: {item.Title}",
                    item.Content.Length > 100 ? item.Content.Substring(0, 100) + "..." : item.Content,
                    "/Notifications/Index");
            }
        }

        Toast("MSG08", isNew ? "Đã đăng thông báo thành công." : "Đã cập nhật thông báo.", "success");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var item = await Db.Announcements.FindAsync(id);
        if (item != null)
        {
            item.IsActive = !item.IsActive;
            await Db.SaveChangesAsync();
            await _auditService.LogAsync(
                "ToggleAnnouncementStatus",
                nameof(Announcement),
                id.ToString(),
                $"Chuyển trạng thái thông báo '{item.Title}' sang IsActive={item.IsActive}");
            Toast("MSG08");
        }
        return RedirectToPage();
    }
}
