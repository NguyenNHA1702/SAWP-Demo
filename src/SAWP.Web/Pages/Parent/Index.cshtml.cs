using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Parent;

public class IndexModel : AppPageModel
{
    public Guardian? GuardianProfile { get; set; }
    public List<Student> Children { get; set; } = new();
    public List<LunchRegistration> TodayLunchList { get; set; } = new();
    public List<AdministrativeRequest> RecentRequests { get; set; } = new();

    public async Task OnGetAsync()
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return;

        GuardianProfile = await Db.Guardians.FindAsync(guardianId.Value);

        // Linked children
        Children = await Db.StudentGuardians
            .Where(sg => sg.GuardianId == guardianId.Value)
            .Include(sg => sg.Student)
            .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
            .ThenInclude(ca => ca.Class)
            .Select(sg => sg.Student)
            .ToListAsync();

        var childIds = Children.Select(c => c.Id).ToList();
        var today = Clock.Today;

        // Today's lunch
        TodayLunchList = await Db.LunchRegistrations
            .Where(r => childIds.Contains(r.StudentId) && r.Date == today)
            .Include(r => r.Student)
            .ToListAsync();

        // Recent requests
        RecentRequests = await Db.AdministrativeRequests
            .Where(r => r.RequesterGuardianId == guardianId.Value)
            .Include(r => r.Student)
            .OrderByDescending(r => r.SubmittedAt)
            .Take(5)
            .ToListAsync();
    }
}
