using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Parent.Attendance;

public class IndexModel : AppPageModel
{
    public List<Student> Children { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ChildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.Today.Month;

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.Today.Year;

    public Student? SelectedChild { get; set; }
    public List<AttendanceRecord> MonthlyRecords { get; set; } = new();
    public int PresentCount { get; set; }
    public int AbsentWithLeaveCount { get; set; }
    public int AbsentWithoutLeaveCount { get; set; }
    public int LateCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return RedirectToPage("/Parent/Index");

        Children = await Db.StudentGuardians
            .Where(sg => sg.GuardianId == guardianId.Value)
            .Include(sg => sg.Student)
            .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
            .ThenInclude(ca => ca.Class)
            .Select(sg => sg.Student)
            .ToListAsync();

        if (!Children.Any()) return Page();

        SelectedChild = ChildId.HasValue
            ? Children.FirstOrDefault(c => c.Id == ChildId.Value)
            : Children.FirstOrDefault();

        if (SelectedChild != null)
        {
            ChildId = SelectedChild.Id;
            var startDate = new DateTime(Year, Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            MonthlyRecords = await Db.AttendanceRecords
                .Where(a => a.StudentId == SelectedChild.Id && a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();

            PresentCount = MonthlyRecords.Count(a => a.Status == AttendanceStatus.Present);
            AbsentWithLeaveCount = MonthlyRecords.Count(a => a.Status == AttendanceStatus.AbsentWithLeave);
            AbsentWithoutLeaveCount = MonthlyRecords.Count(a => a.Status == AttendanceStatus.AbsentWithoutLeave);
            LateCount = MonthlyRecords.Count(a => a.Status == AttendanceStatus.Late);
        }

        return Page();
    }
}
