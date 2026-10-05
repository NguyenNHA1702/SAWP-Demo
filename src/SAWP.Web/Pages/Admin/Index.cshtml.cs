using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Admin;

public class IndexModel : AppPageModel
{
    public int TotalUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalAuditLogs { get; set; }
    public int TotalEmailsOutbox { get; set; }
    public List<AuditLog> RecentAuditLogs { get; set; } = new();

    public async Task OnGetAsync()
    {
        TotalUsers = await Db.Users.CountAsync();
        TotalRoles = await Db.Roles.CountAsync();
        TotalAuditLogs = await Db.AuditLogs.CountAsync();
        TotalEmailsOutbox = await Db.EmailOutboxes.CountAsync();

        RecentAuditLogs = await Db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(6)
            .ToListAsync();
    }
}
