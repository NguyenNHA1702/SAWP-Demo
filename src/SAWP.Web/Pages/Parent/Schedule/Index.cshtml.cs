using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Parent.Schedule;

public class IndexModel : AppPageModel
{
    public List<Student> Children { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ChildId { get; set; }

    public Student? SelectedChild { get; set; }
    public SchoolClass? ChildClass { get; set; }
    public List<ClassSchedule> ScheduleEntries { get; set; } = new();

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

        if (!Children.Any())
        {
            return Page();
        }

        SelectedChild = ChildId.HasValue
            ? Children.FirstOrDefault(c => c.Id == ChildId.Value)
            : Children.FirstOrDefault();

        if (SelectedChild != null)
        {
            ChildId = SelectedChild.Id;
            ChildClass = SelectedChild.ClassAssignments.FirstOrDefault()?.Class;

            if (ChildClass != null)
            {
                ScheduleEntries = await Db.ClassSchedules
                    .Where(s => s.ClassId == ChildClass.Id)
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.PeriodNumber)
                    .ToListAsync();
            }
        }

        return Page();
    }
}
