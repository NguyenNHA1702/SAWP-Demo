using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// Workflow engine for administrative requests (BR-35, BR-36, BR-41).
/// Guarantees that every status change creates RequestStatusHistory and triggers notifications.
/// </summary>
public interface IRequestWorkflowService
{
    Task<AdministrativeRequest> TransitionStatusAsync(
        int requestId,
        RequestStatus newStatus,
        string? actionNotes = null,
        string? newAssigneeRole = null);
}

/// <summary>
/// Implementation of IRequestWorkflowService.
/// </summary>
public class RequestWorkflowService : IRequestWorkflowService
{
    private readonly SawpDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notificationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<RequestWorkflowService> _logger;

    public RequestWorkflowService(
        SawpDbContext db,
        ICurrentUser currentUser,
        INotificationService notificationService,
        IAuditService auditService,
        ILogger<RequestWorkflowService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AdministrativeRequest> TransitionStatusAsync(
        int requestId,
        RequestStatus newStatus,
        string? actionNotes = null,
        string? newAssigneeRole = null)
    {
        var request = await _db.AdministrativeRequests
            .Include(r => r.Student)
            .Include(r => r.RequesterGuardian)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new InvalidOperationException($"Request {requestId} not found.");

        var oldStatus = request.Status;
        if (oldStatus == newStatus)
        {
            return request;
        }

        // Validate cancel status (BR-36)
        if (newStatus == RequestStatus.Cancelled)
        {
            if (oldStatus != RequestStatus.Submitted && oldStatus != RequestStatus.Draft &&
                oldStatus != RequestStatus.InReview && oldStatus != RequestStatus.Escalated)
            {
                throw new InvalidOperationException(Messages.Get("MSG34"));
            }
        }

        request.Status = newStatus;
        if (newAssigneeRole != null)
        {
            request.CurrentAssigneeRole = newAssigneeRole;
        }

        if (newStatus is RequestStatus.Approved or RequestStatus.Rejected or RequestStatus.Completed)
        {
            request.DecidedAt = DateTime.UtcNow;
            request.DecidedByUserId = _currentUser.UserId;
            if (newStatus == RequestStatus.Rejected && !string.IsNullOrWhiteSpace(actionNotes))
            {
                request.RejectionReason = actionNotes;
            }
        }

        // Record history
        var history = new RequestStatusHistory
        {
            AdministrativeRequestId = request.Id,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            ChangedByUserId = _currentUser.UserId,
            ChangedAt = DateTime.UtcNow,
            ActionNotes = actionNotes
        };
        _db.RequestStatusHistories.Add(history);

        await _db.SaveChangesAsync();

        // Audit log (BR-45)
        await _auditService.LogAsync(
            action: "StatusChange",
            entityName: nameof(AdministrativeRequest),
            entityId: request.Id.ToString(),
            details: $"From {oldStatus} to {newStatus}. Notes: {actionNotes}");

        // In-system notification (BR-42)
        if (request.RequesterGuardian?.UserId.HasValue == true)
        {
            var notifTitle = newStatus switch
            {
                RequestStatus.Approved => Messages.Get("MSG31"),
                RequestStatus.Rejected => Messages.Get("MSG32"),
                RequestStatus.Completed => Messages.Get("MSG33"),
                RequestStatus.InReview => "Yêu cầu đang được xử lý",
                RequestStatus.Escalated => "Yêu cầu đã được chuyển lên Ban Giám hiệu",
                _ => $"Cập nhật trạng thái yêu cầu {request.RequestCode}"
            };

            await _notificationService.NotifyUserAsync(
                userId: request.RequesterGuardian.UserId.Value,
                title: notifTitle,
                message: $"Yêu cầu {request.RequestCode} của học sinh {request.Student?.FullName} đã chuyển sang trạng thái: {newStatus}. {actionNotes}".Trim(),
                linkUrl: $"/Parent/Requests/Detail?id={request.Id}");
        }

        _logger.LogInformation(
            "[Workflow] Request={Code} transitioned from {Old} to {New} by User={UserId}",
            request.RequestCode, oldStatus, newStatus, _currentUser.UserId);

        return request;
    }
}
