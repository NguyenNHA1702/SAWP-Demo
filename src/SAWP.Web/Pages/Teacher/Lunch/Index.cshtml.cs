using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Teacher.Lunch;

public class IndexModel : AppPageModel
{
    public SchoolClass? HomeroomClass { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    public LunchServiceDay? ServiceDay { get; set; }
    public int TotalMeals { get; set; }
    public List<StudentLunchStatusDto> Students { get; set; } = new();

    public class StudentLunchStatusDto
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool HasRegistered { get; set; }
        public string? Note { get; set; }
    }

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

        HomeroomClass = hr.Class;
        if (Date == default) Date = Clock.Today;

        ServiceDay = await Db.LunchServiceDays.FirstOrDefaultAsync(d => d.Date == Date.Date);

        var classStudents = await Db.ClassAssignments
            .Where(ca => ca.ClassId == HomeroomClass.Id && ca.IsActive)
            .Include(ca => ca.Student)
            .Select(ca => ca.Student)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var studentIds = classStudents.Select(s => s.Id).ToList();

        var registrations = await Db.LunchRegistrations
            .Where(r => studentIds.Contains(r.StudentId) && r.Date == Date.Date)
            .ToListAsync();

        foreach (var s in classStudents)
        {
            var reg = registrations.FirstOrDefault(r => r.StudentId == s.Id);
            bool isRegistered = reg != null && reg.Status == LunchRegistrationStatus.Registered;
            if (isRegistered) TotalMeals++;

            Students.Add(new StudentLunchStatusDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                HasRegistered = isRegistered,
                Note = reg?.AdjustmentReason
            });
        }

        return Page();
    }
}
