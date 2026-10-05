using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Parent.Children;

public class IndexModel : AppPageModel
{
    public List<Student> Children { get; set; } = new();

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

        return Page();
    }
}
