using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Admin.EmailOutbox;

public class IndexModel : AppPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<SAWP.Web.Domain.EmailOutbox> Emails { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var query = Db.EmailOutboxes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(e => e.ToEmail.ToLower().Contains(s) ||
                                     e.Subject.ToLower().Contains(s) ||
                                     (e.OtpCode != null && e.OtpCode.Contains(s)));
        }

        Emails = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Page();
    }
}
