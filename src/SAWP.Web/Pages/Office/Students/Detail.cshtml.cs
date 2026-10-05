using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Students;

public class DetailModel : AppPageModel
{
    private readonly IScopeService _scopeService;

    public DetailModel(IScopeService scopeService)
    {
        _scopeService = scopeService;
    }

    public Student Student { get; set; } = null!;
    public SchoolClass? CurrentClass { get; set; }
    public List<StudentGuardian> Guardians { get; set; } = new();
    public List<ClassAssignment> ClassHistory { get; set; } = new();
    public List<AdministrativeRequest> Requests { get; set; } = new();
    public int TotalAttendanceDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentWithLeaveDays { get; set; }
    public int AbsentWithoutLeaveDays { get; set; }
    public int LateDays { get; set; }
    public int TotalLunchMeals { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Scope check (BR-05, BR-06, BR-07)
        if (!await _scopeService.CanAccessStudentAsync(id))
        {
            Toast("MSG10");
            return RedirectToPage("/Office/Students/Index");
        }

        var student = await Db.Students
            .Include(s => s.ClassAssignments)
            .ThenInclude(ca => ca.Class)
            .Include(s => s.StudentGuardians)
            .ThenInclude(sg => sg.Guardian)
            .Include(s => s.AdministrativeRequests)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Office/Students/Index");
        }

        Student = student;
        CurrentClass = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class;
        Guardians = student.StudentGuardians.ToList();
        ClassHistory = student.ClassAssignments.OrderByDescending(ca => ca.StartDate).ToList();
        Requests = student.AdministrativeRequests.OrderByDescending(r => r.SubmittedAt).ToList();

        // Attendance stats
        var attendanceList = await Db.AttendanceRecords
            .Where(a => a.StudentId == id)
            .ToListAsync();

        TotalAttendanceDays = attendanceList.Count;
        PresentDays = attendanceList.Count(a => a.Status == AttendanceStatus.Present);
        AbsentWithLeaveDays = attendanceList.Count(a => a.Status == AttendanceStatus.AbsentWithLeave);
        AbsentWithoutLeaveDays = attendanceList.Count(a => a.Status == AttendanceStatus.AbsentWithoutLeave);
        LateDays = attendanceList.Count(a => a.Status == AttendanceStatus.Late);

        // Lunch stats
        TotalLunchMeals = await Db.LunchRegistrations
            .CountAsync(l => l.StudentId == id && l.Status == LunchRegistrationStatus.Registered);

        return Page();
    }
}
