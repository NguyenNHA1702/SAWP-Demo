using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Board;

public class IndexModel : AppPageModel
{
    public int TotalStudents { get; set; }
    public int EscalatedLeaveCount { get; set; }
    public int TransferApprovalCount { get; set; }
    public int TotalMealsToday { get; set; }
    public List<AdministrativeRequest> BoardApprovalsQueue { get; set; } = new();

    public async Task OnGetAsync()
    {
        TotalStudents = await Db.Students.CountAsync(s => s.Status == "Active");

        var today = Clock.Today;
        TotalMealsToday = await Db.LunchRegistrations
            .CountAsync(r => r.Date == today && r.Status == LunchRegistrationStatus.Registered);

        // Escalated leave requests (> 3 days or flagged for Board, UC-27)
        EscalatedLeaveCount = await Db.AdministrativeRequests
            .CountAsync(r => r.RequestType == RequestType.Leave && 
                            (r.Status == RequestStatus.Escalated || (r.LeaveRequest != null && r.LeaveRequest.IsEscalatedToBoard && r.Status == RequestStatus.Submitted)));

        // Transfer requests awaiting Board decision (UC-38)
        TransferApprovalCount = await Db.AdministrativeRequests
            .CountAsync(r => r.RequestType == RequestType.ClassTransfer && r.Status == RequestStatus.InReview);

        // Combined Board queue
        BoardApprovalsQueue = await Db.AdministrativeRequests
            .Where(r => (r.RequestType == RequestType.Leave && r.Status == RequestStatus.Escalated) ||
                        (r.RequestType == RequestType.ClassTransfer && r.Status == RequestStatus.InReview))
            .Include(r => r.Student)
            .Include(r => r.LeaveRequest)
            .Include(r => r.ClassTransferDetail)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();
    }
}
