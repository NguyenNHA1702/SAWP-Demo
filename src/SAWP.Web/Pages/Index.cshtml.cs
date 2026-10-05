using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages;

public class IndexModel : AppPageModel
{
    public List<Announcement> Announcements { get; set; } = new();

    public async Task OnGetAsync()
    {
        Announcements = await Db.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAt)
            .Take(5)
            .ToListAsync();
    }
}
