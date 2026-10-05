using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Classes;

public class AssignTeacherModel : AppPageModel
{
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public AssignTeacherModel(IAuditService auditService, INotificationService notificationService)
    {
        _auditService = auditService;
        _notificationService = notificationService;
    }

    [BindProperty(SupportsGet = true)]
    public int ClassId { get; set; }

    public SchoolClass Class { get; set; } = null!;
    public HomeroomAssignment? CurrentAssignment { get; set; }
    public List<StaffProfile> Teachers { get; set; } = new();

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public int SelectedStaffId { get; set; }

    [BindProperty]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [BindProperty]
    public string? Note { get; set; }

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
        CurrentAssignment = cls.HomeroomAssignments.FirstOrDefault();

        // Eligible teachers
        Teachers = await Db.StaffProfiles
            .Where(sp => sp.IsActive)
            .OrderBy(sp => sp.FullName)
            .ToListAsync();

        if (CurrentAssignment != null)
        {
            SelectedStaffId = CurrentAssignment.StaffProfileId;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var cls = await Db.SchoolClasses
            .Include(c => c.HomeroomAssignments.Where(ha => ha.IsActive))
            .ThenInclude(ha => ha.StaffProfile)
            .FirstOrDefaultAsync(c => c.Id == ClassId);

        if (cls == null)
        {
            Toast("MSG15");
            return RedirectToPage("/Office/Classes/Index");
        }

        var teacher = await Db.StaffProfiles.FindAsync(SelectedStaffId);
        if (teacher == null)
        {
            Toast("MSG01");
            return await OnGetAsync(ClassId);
        }

        // UC-15 A1: Close prior assignment on effective date (BR-16)
        var priorAssignments = await Db.HomeroomAssignments
            .Where(ha => ha.ClassId == ClassId && ha.IsActive)
            .ToListAsync();

        foreach (var pa in priorAssignments)
        {
            pa.IsActive = false;
            pa.EndDate = StartDate;
        }

        // Create new assignment
        var newAssignment = new HomeroomAssignment
        {
            ClassId = ClassId,
            StaffProfileId = SelectedStaffId,
            StartDate = StartDate,
            IsActive = true,
            Note = Note?.Trim()
        };

        Db.HomeroomAssignments.Add(newAssignment);
        await Db.SaveChangesAsync();

        // Audit log (BR-11)
        await _auditService.LogAsync(
            "AssignTeacher",
            nameof(HomeroomAssignment),
            newAssignment.Id.ToString(),
            $"Phân công giáo viên {teacher.FullName} làm GVCN Lớp {cls.Name} từ ngày {FormatDate(StartDate)}");

        // Notify teacher (BR-42)
        if (teacher.UserId.HasValue)
        {
            await _notificationService.NotifyUserAsync(
                userId: teacher.UserId.Value,
                title: "Phân công chủ nhiệm mới",
                message: $"Bạn đã được phân công làm Giáo viên chủ nhiệm Lớp {cls.Name} từ ngày {FormatDate(StartDate)}.",
                linkUrl: "/Teacher/Index");
        }

        Toast("MSG08", $"Đã phân công GVCN {teacher.FullName} cho Lớp {cls.Name}.", "success");
        return RedirectToPage("/Office/Classes/Index");
    }
}
