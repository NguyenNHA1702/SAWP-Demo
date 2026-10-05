using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Teacher.Leave;

public class IndexModel : AppPageModel
{
    private readonly IScopeService _scopeService;
    private readonly IRequestWorkflowService _workflowService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public IndexModel(
        IScopeService scopeService,
        IRequestWorkflowService workflowService,
        IAuditService auditService,
        INotificationService notificationService)
    {
        _scopeService = scopeService;
        _workflowService = workflowService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public SchoolClass? HomeroomClass { get; set; }
    public List<AdministrativeRequest> PendingRequests { get; set; } = new();
    public List<AdministrativeRequest> ProcessedRequests { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        // BR-07: Homeroom class scope
        var homeroomAssignment = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (homeroomAssignment == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        HomeroomClass = homeroomAssignment.Class;

        // Load students in this class
        var studentIds = await Db.ClassAssignments
            .Where(ca => ca.ClassId == HomeroomClass.Id && ca.IsActive)
            .Select(ca => ca.StudentId)
            .ToListAsync();

        var query = Db.AdministrativeRequests
            .Where(r => studentIds.Contains(r.StudentId) && r.RequestType == RequestType.Leave)
            .Include(r => r.Student)
            .Include(r => r.RequesterGuardian)
            .Include(r => r.LeaveRequest)
            .Include(r => r.StatusHistories);

        // Pending: Status is Submitted or InReview, and NOT escalated to BGH
        PendingRequests = await query
            .Where(r => (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InReview) &&
                        (r.LeaveRequest == null || !r.LeaveRequest.IsEscalatedToBoard))
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        ProcessedRequests = await query
            .Where(r => r.Status == RequestStatus.Approved || r.Status == RequestStatus.Rejected ||
                        r.Status == RequestStatus.Escalated || r.Status == RequestStatus.Cancelled)
            .OrderByDescending(r => r.DecidedAt ?? r.SubmittedAt)
            .Take(30)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, string? notes)
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return RedirectToPage("/Teacher/Index");

        var homeroomAssignment = await Db.HomeroomAssignments
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (homeroomAssignment == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .Include(r => r.LeaveRequest)
            .Include(r => r.RequesterGuardian)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.Leave);

        if (request == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        // Scope check (BR-07)
        if (!await _scopeService.CanAccessStudentAsync(request.StudentId))
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        if (request.LeaveRequest?.IsEscalatedToBoard == true)
        {
            Toast("MSG09", "Đơn này thuộc thẩm quyền phê duyệt của Ban Giám hiệu (BR-25, BR-41).", "warning");
            return RedirectToPage();
        }

        // Transition to Approved
        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Approved,
            actionNotes: string.IsNullOrWhiteSpace(notes) ? "GVCN đã phê duyệt đơn xin nghỉ học" : notes);

        // BR-29: Automatically mark Attendance as ExcusedAbsence for the leave period
        if (request.LeaveRequest != null)
        {
            var start = request.LeaveRequest.StartDate.Date;
            var end = request.LeaveRequest.EndDate.Date;

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday || d.DayOfWeek == DayOfWeek.Saturday)
                    continue;

                var att = await Db.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.StudentId == request.StudentId && a.Date == d);

                if (att == null)
                {
                    att = new AttendanceRecord
                    {
                        StudentId = request.StudentId,
                        ClassId = homeroomAssignment.ClassId,
                        Date = d,
                        Status = AttendanceStatus.AbsentWithLeave,
                        Note = $"Nghỉ có phép (Đơn {request.RequestCode})",
                        MarkedByStaffId = staffId.Value,
                        MarkedAt = DateTime.UtcNow
                    };
                    Db.AttendanceRecords.Add(att);
                }
                else if (att.Status != AttendanceStatus.AbsentWithLeave)
                {
                    att.Status = AttendanceStatus.AbsentWithLeave;
                    att.Note = $"Nghỉ có phép (Đơn {request.RequestCode})";
                    att.MarkedByStaffId = staffId.Value;
                    att.MarkedAt = DateTime.UtcNow;
                }

                // BR-29: Automatically cancel lunch for those days
                var lunchReg = await Db.LunchRegistrations
                    .FirstOrDefaultAsync(l => l.StudentId == request.StudentId && l.Date == d && l.Status == LunchRegistrationStatus.Registered);
                if (lunchReg != null)
                {
                    lunchReg.Status = LunchRegistrationStatus.Cancelled;
                    lunchReg.CancelledAt = DateTime.UtcNow;
                    lunchReg.AdjustmentReason = $"Hủy theo đơn xin nghỉ {request.RequestCode}";
                }
            }

            await Db.SaveChangesAsync();
        }

        await _auditService.LogAsync(
            "ApproveLeave",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"GVCN duyệt đơn nghỉ {request.RequestCode} cho học sinh {request.Student?.FullName}");

        Toast("MSG22");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason)
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return RedirectToPage("/Teacher/Index");

        if (string.IsNullOrWhiteSpace(reason))
        {
            Toast("MSG01", "Vui lòng nhập lý do từ chối đơn xin nghỉ.", "error");
            return RedirectToPage();
        }

        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .Include(r => r.LeaveRequest)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.Leave);

        if (request == null || !await _scopeService.CanAccessStudentAsync(request.StudentId))
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Rejected,
            actionNotes: reason.Trim());

        await _auditService.LogAsync(
            "RejectLeave",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"GVCN từ chối đơn nghỉ {request.RequestCode}. Lý do: {reason.Trim()}");

        Toast("MSG23");
        return RedirectToPage();
    }
}
