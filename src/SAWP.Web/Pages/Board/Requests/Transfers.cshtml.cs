using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Board.Requests;

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

    public List<AdministrativeRequest> PendingRequests { get; set; } = new();
    public List<AdministrativeRequest> ProcessedRequests { get; set; } = new();

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

        PendingRequests = await query
            .Where(r => r.Status == RequestStatus.Escalated || r.Status == RequestStatus.InReview)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        ProcessedRequests = await query
            .Where(r => r.Status == RequestStatus.Approved || r.Status == RequestStatus.Completed ||
                        r.Status == RequestStatus.Rejected || r.Status == RequestStatus.Cancelled)
            .OrderByDescending(r => r.DecidedAt ?? r.SubmittedAt)
            .Take(30)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, string? notes)
    {
        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.TargetClass)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.ClassTransfer);

        if (request == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        // BR-38: BGH approves transfer, forwards to Office to execute
        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Approved,
            actionNotes: string.IsNullOrWhiteSpace(notes) ? "Ban Giám hiệu đã phê duyệt nguyện vọng chuyển lớp" : notes,
            newAssigneeRole: nameof(SystemRole.Office));

        await _auditService.LogAsync(
            "BoardApproveTransfer",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Ban Giám hiệu phê duyệt chuyển lớp cho học sinh {request.Student?.FullName}. Đã chuyển Giáo vụ phân lớp.");

        await _notificationService.NotifyRoleAsync(
            nameof(SystemRole.Office),
            "Hồ sơ chuyển lớp đã được BGH phê duyệt",
            $"BGH đã duyệt đơn chuyển lớp {request.RequestCode} cho học sinh {request.Student?.FullName}. Vui lòng thực thi phân lớp.",
            "/Office/Requests/Transfers");

        Toast("MSG31");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Toast("MSG01", "Vui lòng nhập lý do từ chối của Ban Giám hiệu.", "error");
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
            "BoardRejectTransfer",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Ban Giám hiệu từ chối đơn chuyển lớp {request.RequestCode}. Lý do: {reason.Trim()}");

        Toast("MSG32");
        return RedirectToPage();
    }
}
