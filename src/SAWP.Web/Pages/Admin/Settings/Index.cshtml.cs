using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Admin.Settings;

public class IndexModel : AppPageModel
{
    private readonly ISettingsService _settingsService;
    private readonly IAuditService _auditService;

    public IndexModel(ISettingsService settingsService, IAuditService auditService)
    {
        _settingsService = settingsService;
        _auditService = auditService;
    }

    public List<SystemSetting> AllSettings { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        AllSettings = await Db.SystemSettings
            .OrderBy(s => s.Key)
            .ToListAsync();

        // Ensure key default settings exist if missing
        if (!AllSettings.Any(s => s.Key == "Leave.MaxDaysForTeacher"))
        {
            await _settingsService.SetAsync("Leave.MaxDaysForTeacher", "3", "Số ngày nghỉ tối đa do GVCN trực tiếp phê duyệt (vượt quá chuyển BGH)");
        }
        if (!AllSettings.Any(s => s.Key == "Attendance.TeacherEditWindowHours"))
        {
            await _settingsService.SetAsync("Attendance.TeacherEditWindowHours", "24", "Thời hạn GVCN được phép tự điều chỉnh điểm danh (giờ)");
        }
        if (!AllSettings.Any(s => s.Key == "Lunch.DefaultCutoffTime"))
        {
            await _settingsService.SetAsync("Lunch.DefaultCutoffTime", "08:00", "Giờ chốt đăng ký và hủy suất ăn bán trú buổi sáng");
        }
        if (!AllSettings.Any(s => s.Key == "School.Name"))
        {
            await _settingsService.SetAsync("School.Name", "Trường Tiểu học Thực nghiệm SAWP", "Tên trường tiểu học");
        }
        if (!AllSettings.Any(s => s.Key == "School.Phone"))
        {
            await _settingsService.SetAsync("School.Phone", "024.3823.4567", "Số điện thoại liên hệ");
        }

        AllSettings = await Db.SystemSettings.OrderBy(s => s.Key).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Dictionary<string, string> settings)
    {
        // UC-59: Update system settings (MSG44)
        if (settings != null)
        {
            foreach (var kvp in settings)
            {
                await _settingsService.SetAsync(kvp.Key, kvp.Value.Trim());
            }

            await _auditService.LogAsync(
                "UpdateSystemSettings",
                nameof(SystemSetting),
                "Bulk",
                $"Cập nhật {settings.Count} thông số cấu hình hệ thống");
        }

        Toast("MSG44");
        return RedirectToPage();
    }
}
