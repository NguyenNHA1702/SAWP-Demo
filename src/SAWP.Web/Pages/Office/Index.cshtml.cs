using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Office;

public class IndexModel : AppPageModel
{
    public int TotalStudents { get; set; }
    public int TotalClasses { get; set; }
    public int PendingTransferRequests { get; set; }
    public int PendingDocumentRequests { get; set; }
    public List<AdministrativeRequest> PendingRequests { get; set; } = new();

    public async Task OnGetAsync()
    {
        TotalStudents = await Db.Students.CountAsync(s => s.Status == "Active");
        TotalClasses = await Db.SchoolClasses.CountAsync(c => c.IsActive);

        PendingTransferRequests = await Db.AdministrativeRequests
            .CountAsync(r => r.RequestType == RequestType.ClassTransfer && r.Status == RequestStatus.Submitted);

        PendingDocumentRequests = await Db.AdministrativeRequests
            .CountAsync(r => r.RequestType == RequestType.DocumentIssuance && r.Status == RequestStatus.Submitted);

        PendingRequests = await Db.AdministrativeRequests
            .Where(r => r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InReview)
            .Include(r => r.Student)
            .Include(r => r.DocumentRequestDetail)
            .Include(r => r.ClassTransferDetail)
            .OrderByDescending(r => r.SubmittedAt)
            .Take(8)
            .ToListAsync();
    }
}
