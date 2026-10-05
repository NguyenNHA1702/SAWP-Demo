using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Requests;

public class TransfersModel : AppPageModel
{
    private readonly IRequestWorkflowService _workflowService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public TransfersModel(
        IRequestWorkflowService workflowService,
        IAuditService auditService,
        INotificationService notificationService)
    {
        _workflowService = workflowService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public List<AdministrativeRequest> PendingReviewRequests { get; set; } = new();
    public List<AdministrativeRequest> ApprovedPendingApplyRequests { get; set; } = new();
    public List<AdministrativeRequest> HistoryRequests { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var query = Db.AdministrativeRequests
            .Where(r => r.RequestType == RequestType.ClassTransfer)
            .Include(r => r.Student)
            .Include(r => r.RequesterGuardian)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.CurrentClass)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.TargetClass)
            .Include(r => r.StatusHistories);

        // Pending review by Office (to forward to Board)
        PendingReviewRequests = await query
            .Where(r => r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InReview)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        // Approved by Board, waiting for Office to apply transfer (UC-39)
        ApprovedPendingApplyRequests = await query
            .Where(r => r.Status == RequestStatus.Approved && r.ClassTransferDetail != null && !r.ClassTransferDetail.IsApplied)
            .OrderBy(r => r.DecidedAt ?? r.SubmittedAt)
            .ToListAsync();

        HistoryRequests = await query
            .Where(r => r.Status == RequestStatus.Completed || r.Status == RequestStatus.Rejected ||
                        r.Status == RequestStatus.Cancelled ||
                        (r.Status == RequestStatus.Escalated && r.CurrentAssigneeRole == nameof(SystemRole.Board)))
            .OrderByDescending(r => r.DecidedAt ?? r.SubmittedAt)
            .Take(30)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostForwardToBoardAsync(int id, string? notes)
    {
        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.ClassTransfer);

        if (request == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Escalated,
            actionNotes: string.IsNullOrWhiteSpace(notes) ? "Giáo vụ đã thẩm tra điều kiện và trình Ban Giám hiệu phê duyệt" : notes,
            newAssigneeRole: nameof(SystemRole.Board));

        await _auditService.LogAsync(
            "ForwardTransferToBoard",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Giáo vụ chuyển đơn {request.RequestCode} của học sinh {request.Student?.FullName} lên BGH");

        await _notificationService.NotifyRoleAsync(
            nameof(SystemRole.Board),
            "Đơn xin chuyển lớp cần BGH phê duyệt",
            $"Hồ sơ chuyển lớp {request.RequestCode} của học sinh {request.Student?.FullName} đã được Giáo vụ thẩm tra và trình duyệt.",
            "/Board/Requests/Transfers");

        Toast("MSG08", "Đã trình đơn lên Ban Giám hiệu xét duyệt.", "success");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApplyTransferAsync(int id)
    {
        // UC-39: Apply class transfer (BR-37, BR-38, MSG35)
        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.CurrentClass)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.TargetClass)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.ClassTransfer);

        if (request == null || request.ClassTransferDetail == null || request.Status != RequestStatus.Approved)
        {
            Toast("MSG09");
            return RedirectToPage();
        }

        var detail = request.ClassTransferDetail;
        var student = request.Student;

        // Deactivate old active class assignment
        var oldAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive && ca.ClassId == detail.CurrentClassId);
        if (oldAssignment != null)
        {
            oldAssignment.IsActive = false;
            oldAssignment.EndDate = DateTime.UtcNow;
        }

        // Create new active class assignment
        var newAssignment = new ClassAssignment
        {
            StudentId = student.Id,
            ClassId = detail.TargetClassId,
            StartDate = DateTime.UtcNow,
            IsActive = true
        };
        Db.ClassAssignments.Add(newAssignment);

        detail.IsApplied = true;
        detail.EffectiveDate = DateTime.UtcNow;

        // Complete workflow
        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Completed,
            actionNotes: $"Giáo vụ đã cập nhật phân lớp chính thức sang {detail.TargetClass.Name}");

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "ApplyClassTransfer",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Thực thi chuyển lớp cho học sinh {student.FullName}: {detail.CurrentClass.Name} -> {detail.TargetClass.Name}");

        Toast("MSG35");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Toast("MSG01", "Vui lòng nhập lý do từ chối đơn chuyển lớp.", "error");
            return RedirectToPage();
        }

        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.ClassTransfer);

        if (request == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Rejected,
            actionNotes: reason.Trim());

        await _auditService.LogAsync(
            "OfficeRejectTransfer",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Giáo vụ từ chối đơn chuyển lớp {request.RequestCode}. Lý do: {reason.Trim()}");

        Toast("MSG32");
        return RedirectToPage();
    }
}
