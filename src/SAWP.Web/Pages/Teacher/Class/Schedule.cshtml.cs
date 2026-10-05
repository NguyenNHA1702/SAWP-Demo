using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Teacher.Class;

public class ScheduleModel : AppPageModel
{
    public SchoolClass? AssignedClass { get; set; }
    public List<ClassSchedule> ScheduleEntries { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return RedirectToPage("/Teacher/Index");

        var hr = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (hr == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        AssignedClass = hr.Class;
        ScheduleEntries = await Db.ClassSchedules
            .Where(s => s.ClassId == AssignedClass.Id)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.PeriodNumber)
            .ToListAsync();

        return Page();
    }
}
