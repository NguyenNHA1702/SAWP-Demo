using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Service;

public class IndexModel : AppPageModel
{
    public LunchServiceDay? TodayLunchDay { get; set; }
    public int TotalMealsToday { get; set; }
    public bool IsPastCutoff { get; set; }
    public List<ClassMealSummary> ClassSummaries { get; set; } = new();

    public class ClassMealSummary
    {
        public string ClassName { get; set; } = string.Empty;
        public int RegisteredCount { get; set; }
        public int Capacity { get; set; }
    }

    public async Task OnGetAsync()
    {
        var today = Clock.Today;
        TodayLunchDay = await Db.LunchServiceDays.FirstOrDefaultAsync(l => l.Date == today);

        if (TodayLunchDay != null)
        {
            var cutoffTimeToday = today.Add(TodayLunchDay.CutoffTime);
            IsPastCutoff = Clock.LocalNow >= cutoffTimeToday;

            TotalMealsToday = await Db.LunchRegistrations
                .CountAsync(r => r.Date == today && r.Status == LunchRegistrationStatus.Registered);

            // Group by class
            var activeClasses = await Db.SchoolClasses
                .Where(c => c.IsActive)
                .OrderBy(c => c.Grade)
                .ThenBy(c => c.Name)
                .ToListAsync();

            foreach (var cls in activeClasses)
            {
                var count = await Db.LunchRegistrations
                    .Where(r => r.Date == today && r.Status == LunchRegistrationStatus.Registered)
                    .CountAsync(r => Db.ClassAssignments
                        .Any(ca => ca.ClassId == cls.Id && ca.StudentId == r.StudentId && ca.IsActive));

                ClassSummaries.Add(new ClassMealSummary
                {
                    ClassName = cls.Name,
                    RegisteredCount = count,
                    Capacity = cls.Capacity
                });
            }
        }
    }
}
