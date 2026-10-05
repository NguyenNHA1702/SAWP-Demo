using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Requests;

public class DocumentsModel : AppPageModel
{
    private readonly IRequestWorkflowService _workflowService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public DocumentsModel(
        IRequestWorkflowService workflowService,
        IAuditService auditService,
        INotificationService notificationService)
    {
        _workflowService = workflowService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public List<AdministrativeRequest> PendingRequests { get; set; } = new();
    public List<AdministrativeRequest> CompletedRequests { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var query = Db.AdministrativeRequests
            .Where(r => r.RequestType == RequestType.DocumentIssuance)
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Include(r => r.RequesterGuardian)
            .Include(r => r.DocumentRequestDetail)
            .Include(r => r.IssuedDocument)
            .Include(r => r.StatusHistories);

        PendingRequests = await query
            .Where(r => r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InReview)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        CompletedRequests = await query
            .Where(r => r.Status == RequestStatus.Completed || r.Status == RequestStatus.Approved ||
                        r.Status == RequestStatus.Rejected || r.Status == RequestStatus.Cancelled)
            .OrderByDescending(r => r.DecidedAt ?? r.SubmittedAt)
            .Take(30)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostIssueAsync(int id, string? summary)
    {
        // UC-42: Issue school document (BR-39, BR-40, MSG36)
        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .Include(r => r.DocumentRequestDetail)
            .Include(r => r.RequesterGuardian)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.DocumentIssuance);

        if (request == null || request.DocumentRequestDetail == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        var docCount = await Db.IssuedDocuments.CountAsync() + 1;
        var docNumber = $"GXN-{Clock.Today.Year}/{docCount:D4}";

        var issuedDoc = new IssuedDocument
        {
            AdministrativeRequestId = request.Id,
            DocumentNumber = docNumber,
            DocumentType = request.DocumentRequestDetail.DocumentType,
            IssueDate = DateTime.UtcNow,
            IssuedByStaffId = staffId,
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            ContentSummary = string.IsNullOrWhiteSpace(summary)
                ? $"Xác nhận học sinh {request.Student.FullName} - Mục đích: {request.DocumentRequestDetail.Purpose} (Số lượng: {request.DocumentRequestDetail.Quantity} bản)"
                : summary.Trim(),
            FileAttachmentUrl = $"/documents/generated/{docNumber}.pdf"
        };
        Db.IssuedDocuments.Add(issuedDoc);

        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Completed,
            actionNotes: $"Đã phát hành văn bản số {docNumber}. Phụ huynh có thể nhận bản giấy tại phòng Giáo vụ.");

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "IssueDocument",
            nameof(IssuedDocument),
            issuedDoc.Id.ToString(),
            $"Giáo vụ phát hành {issuedDoc.DocumentType} số {docNumber} cho học sinh {request.Student.FullName}");

        if (request.RequesterGuardian?.UserId.HasValue == true)
        {
            await _notificationService.NotifyUserAsync(
                request.RequesterGuardian.UserId.Value,
                "Giấy xác nhận đã được phát hành",
                $"Nhà trường đã cấp {issuedDoc.DocumentType} số {docNumber} cho học sinh {request.Student.FullName}. Kính mời phụ huynh đến phòng Giáo vụ để nhận bản đóng dấu.",
                $"/Parent/Requests/Index");
        }

        Toast("MSG36");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Toast("MSG01", "Vui lòng nhập lý do từ chối yêu cầu cấp giấy tờ.", "error");
            return RedirectToPage();
        }

        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.DocumentIssuance);

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
            "RejectDocumentRequest",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Giáo vụ từ chối yêu cầu cấp giấy tờ {request.RequestCode}. Lý do: {reason.Trim()}");

        Toast("MSG32");
        return RedirectToPage();
    }
}
