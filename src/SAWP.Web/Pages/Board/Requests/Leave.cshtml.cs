using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Board.Requests;

public class LeaveModel : AppPageModel
{
    private readonly IRequestWorkflowService _workflowService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public LeaveModel(
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
            .Where(r => r.RequestType == RequestType.Leave &&
                        (r.Status == RequestStatus.Escalated ||
                         (r.LeaveRequest != null && r.LeaveRequest.IsEscalatedToBoard)))
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Include(r => r.RequesterGuardian)
            .Include(r => r.LeaveRequest)
            .Include(r => r.StatusHistories);

        PendingRequests = await query
            .Where(r => r.Status == RequestStatus.Escalated || r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InReview)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        ProcessedRequests = await query
            .Where(r => r.Status == RequestStatus.Approved || r.Status == RequestStatus.Rejected || r.Status == RequestStatus.Cancelled)
            .OrderByDescending(r => r.DecidedAt ?? r.SubmittedAt)
            .Take(30)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, string? notes)
    {
        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Include(r => r.LeaveRequest)
            .Include(r => r.RequesterGuardian)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.Leave);

        if (request == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        await _workflowService.TransitionStatusAsync(
            request.Id,
            RequestStatus.Approved,
            actionNotes: string.IsNullOrWhiteSpace(notes) ? "Ban Giám hiệu đã phê duyệt đơn xin nghỉ học dài ngày" : notes);

        // BR-29: Automatically mark Attendance as ExcusedAbsence for the leave period
        if (request.LeaveRequest != null)
        {
            var start = request.LeaveRequest.StartDate.Date;
            var end = request.LeaveRequest.EndDate.Date;

            // Find student's active class assignment
            var activeClassAssignment = request.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
            int classId = activeClassAssignment?.ClassId ?? 1;

            var staffId = await CurrentUser.GetStaffProfileIdAsync();

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
                        ClassId = classId,
                        Date = d,
                        Status = AttendanceStatus.AbsentWithLeave,
                        Note = $"Nghỉ có phép theo quyết định BGH (Đơn {request.RequestCode})",
                        MarkedByStaffId = staffId,
                        MarkedAt = DateTime.UtcNow
                    };
                    Db.AttendanceRecords.Add(att);
                }
                else if (att.Status != AttendanceStatus.AbsentWithLeave)
                {
                    att.Status = AttendanceStatus.AbsentWithLeave;
                    att.Note = $"Nghỉ có phép theo quyết định BGH (Đơn {request.RequestCode})";
                    att.MarkedAt = DateTime.UtcNow;
                }

                // BR-29: Automatically cancel lunch for those days
                var lunchReg = await Db.LunchRegistrations
                    .FirstOrDefaultAsync(l => l.StudentId == request.StudentId && l.Date == d && l.Status == LunchRegistrationStatus.Registered);
                if (lunchReg != null)
                {
                    lunchReg.Status = LunchRegistrationStatus.Cancelled;
                    lunchReg.CancelledAt = DateTime.UtcNow;
                    lunchReg.AdjustmentReason = $"Hủy theo đơn xin nghỉ {request.RequestCode} đã được BGH duyệt";
                }
            }

            await Db.SaveChangesAsync();
        }

        await _auditService.LogAsync(
            "BoardApproveLeave",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Ban Giám hiệu duyệt đơn nghỉ {request.RequestCode} ({request.LeaveRequest?.TotalDays} ngày) cho học sinh {request.Student?.FullName}");

        // Notify homeroom teacher if exists
        var studentClassId = request.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.ClassId;
        if (studentClassId.HasValue)
        {
            var hr = await Db.HomeroomAssignments
                .Include(h => h.StaffProfile)
                .FirstOrDefaultAsync(h => h.ClassId == studentClassId.Value && h.IsActive);
            if (hr?.StaffProfile?.UserId.HasValue == true)
            {
                await _notificationService.NotifyUserAsync(
                    hr.StaffProfile.UserId.Value,
                    "Thông báo: BGH đã duyệt đơn xin nghỉ dài ngày",
                    $"Đơn nghỉ {request.RequestCode} của học sinh {request.Student?.FullName} đã được Ban Giám hiệu phê duyệt.",
                    "/Teacher/Leave/Index");
            }
        }

        Toast("MSG22");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Toast("MSG01", "Vui lòng nhập lý do từ chối đơn xin nghỉ.", "error");
            return RedirectToPage();
        }

        var request = await Db.AdministrativeRequests
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == id && r.RequestType == RequestType.Leave);

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
            "BoardRejectLeave",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Ban Giám hiệu từ chối đơn nghỉ {request.RequestCode}. Lý do: {reason.Trim()}");

        Toast("MSG23");
        return RedirectToPage();
    }
}
