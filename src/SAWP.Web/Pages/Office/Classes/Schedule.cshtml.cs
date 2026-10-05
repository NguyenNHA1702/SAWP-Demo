using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Classes;

public class ScheduleModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public ScheduleModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public int ClassId { get; set; }

    public SchoolClass Class { get; set; } = null!;
    public List<ClassSchedule> ScheduleEntries { get; set; } = new();

    // Slot entry form
    [BindProperty]
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    [BindProperty]
    public int PeriodNumber { get; set; } = 1;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string SubjectName { get; set; } = string.Empty;

    [BindProperty]
    public string? TeacherName { get; set; }

    [BindProperty]
    public string? Room { get; set; }

    public async Task<IActionResult> OnGetAsync(int classId)
    {
        ClassId = classId;
        var cls = await Db.SchoolClasses
            .Include(c => c.HomeroomAssignments.Where(ha => ha.IsActive))
            .ThenInclude(ha => ha.StaffProfile)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (cls == null)
        {
            Toast("MSG15");
            return RedirectToPage("/Office/Classes/Index");
        }

        Class = cls;
        ScheduleEntries = await Db.ClassSchedules
            .Where(s => s.ClassId == classId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.PeriodNumber)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAddSlotAsync()
    {
        if (string.IsNullOrWhiteSpace(SubjectName) || PeriodNumber < 1 || PeriodNumber > 8)
        {
            Toast("MSG01");
            return await OnGetAsync(ClassId);
        }

        // UC-18 E1: Check time-slot conflict (BR-19)
        var existing = await Db.ClassSchedules
            .FirstOrDefaultAsync(s => s.ClassId == ClassId && s.DayOfWeek == DayOfWeek && s.PeriodNumber == PeriodNumber);

        if (existing != null)
        {
            // Replace existing slot
            existing.SubjectName = SubjectName.Trim();
            existing.TeacherName = TeacherName?.Trim();
            existing.Room = Room?.Trim();
        }
        else
        {
            var slot = new ClassSchedule
            {
                ClassId = ClassId,
                DayOfWeek = DayOfWeek,
                PeriodNumber = PeriodNumber,
                SubjectName = SubjectName.Trim(),
                TeacherName = TeacherName?.Trim(),
                Room = Room?.Trim()
            };
            Db.ClassSchedules.Add(slot);
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UpdateSchedule",
            nameof(ClassSchedule),
            ClassId.ToString(),
            $"Cập nhật TKB Lớp {ClassId}: {DayOfWeek} Tiết {PeriodNumber} môn {SubjectName} (UC-18)");

        Toast("MSG08");
        return RedirectToPage("/Office/Classes/Schedule", new { classId = ClassId });
    }

    public async Task<IActionResult> OnPostDeleteSlotAsync(int slotId)
    {
        var slot = await Db.ClassSchedules.FirstOrDefaultAsync(s => s.Id == slotId && s.ClassId == ClassId);
        if (slot != null)
        {
            Db.ClassSchedules.Remove(slot);
            await Db.SaveChangesAsync();
            Toast("MSG08", "Đã xóa tiết học khỏi thời khóa biểu.", "success");
        }

        return RedirectToPage("/Office/Classes/Schedule", new { classId = ClassId });
    }
}
