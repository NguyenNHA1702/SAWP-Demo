using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Classes;

public class AssignStudentsModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public AssignStudentsModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public int ClassId { get; set; }

    public SchoolClass Class { get; set; } = null!;
    public List<ClassAssignment> CurrentRoster { get; set; } = new();
    public List<Student> UnassignedStudents { get; set; } = new();

    [BindProperty]
    public List<int> SelectedStudentIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int classId)
    {
        ClassId = classId;
        var cls = await Db.SchoolClasses
            .Include(c => c.SchoolYear)
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
            .ThenInclude(ca => ca.Student)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (cls == null)
        {
            Toast("MSG15");
            return RedirectToPage("/Office/Classes/Index");
        }

        Class = cls;
        CurrentRoster = cls.ClassAssignments.OrderBy(ca => ca.RollNumber ?? 999).ThenBy(ca => ca.Student.FullName).ToList();

        // Find students without any active class assignment in this school year
        var assignedStudentIdsInYear = await Db.ClassAssignments
            .Where(ca => ca.Class.SchoolYearId == cls.SchoolYearId && ca.IsActive)
            .Select(ca => ca.StudentId)
            .ToListAsync();

        UnassignedStudents = await Db.Students
            .Where(s => s.Status == "Active" && !assignedStudentIdsInYear.Contains(s.Id))
            .OrderBy(s => s.StudentCode)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync()
    {
        var cls = await Db.SchoolClasses
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
            .FirstOrDefaultAsync(c => c.Id == ClassId);

        if (cls == null)
        {
            Toast("MSG15");
            return RedirectToPage("/Office/Classes/Index");
        }

        if (!SelectedStudentIds.Any())
        {
            Toast("MSG01", "Vui lòng chọn ít nhất một học sinh để phân lớp.", "info");
            return await OnGetAsync(ClassId);
        }

        // UC-16 E1: Capacity check (BR-15)
        var currentCount = cls.ClassAssignments.Count;
        if (currentCount + SelectedStudentIds.Count > cls.Capacity)
        {
            Toast("MSG15");
            return await OnGetAsync(ClassId);
        }

        int nextRoll = currentCount + 1;
        foreach (var sId in SelectedStudentIds)
        {
            var assignment = new ClassAssignment
            {
                StudentId = sId,
                ClassId = ClassId,
                RollNumber = nextRoll++,
                StartDate = DateTime.Today,
                IsActive = true
            };
            Db.ClassAssignments.Add(assignment);
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "AssignStudents",
            nameof(ClassAssignment),
            ClassId.ToString(),
            $"Phân lớp cho {SelectedStudentIds.Count} học sinh vào Lớp {cls.Name} (UC-16)");

        Toast("MSG16");
        return RedirectToPage("/Office/Classes/AssignStudents", new { classId = ClassId });
    }

    public async Task<IActionResult> OnPostRemoveAsync(int assignmentId)
    {
        var assignment = await Db.ClassAssignments
            .Include(ca => ca.Student)
            .Include(ca => ca.Class)
            .FirstOrDefaultAsync(ca => ca.Id == assignmentId && ca.ClassId == ClassId);

        if (assignment != null)
        {
            assignment.IsActive = false;
            assignment.EndDate = DateTime.Today;
            assignment.TransferNote = "Rút khỏi lớp bởi Giáo vụ";
            await Db.SaveChangesAsync();

            await _auditService.LogAsync(
                "RemoveFromClass",
                nameof(ClassAssignment),
                assignmentId.ToString(),
                $"Đưa học sinh {assignment.Student.FullName} ra khỏi Lớp {assignment.Class.Name}");

            Toast("MSG16", "Đã cập nhật danh sách lớp học.", "success");
        }

        return RedirectToPage("/Office/Classes/AssignStudents", new { classId = ClassId });
    }
}
