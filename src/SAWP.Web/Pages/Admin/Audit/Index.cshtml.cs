using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Admin.Audit;

public class IndexModel : AppPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActionFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EntityFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    public List<AuditLog> Logs { get; set; } = new();
    public List<string> AvailableActions { get; set; } = new();
    public List<string> AvailableEntities { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        AvailableActions = await Db.AuditLogs
            .Select(l => l.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        AvailableEntities = await Db.AuditLogs
            .Select(l => l.EntityName)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        var query = Db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var k = Keyword.Trim().ToLower();
            query = query.Where(l => l.ActorName.ToLower().Contains(k) ||
                                     l.EntityId!.Contains(k) ||
                                     l.Details!.ToLower().Contains(k));
        }

        if (!string.IsNullOrWhiteSpace(ActionFilter))
        {
            query = query.Where(l => l.Action == ActionFilter);
        }

        if (!string.IsNullOrWhiteSpace(EntityFilter))
        {
            query = query.Where(l => l.EntityName == EntityFilter);
        }

        if (FromDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= FromDate.Value.Date);
        }

        if (ToDate.HasValue)
        {
            var toEnd = ToDate.Value.Date.AddDays(1);
            query = query.Where(l => l.Timestamp < toEnd);
        }

        Logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync();

        return Page();
    }
}
