using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Parent.Requests;

public class CreateLeaveModel : AppPageModel
{
    private readonly IScopeService _scopeService;
    private readonly ISettingsService _settingsService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public CreateLeaveModel(
        IScopeService scopeService,
        ISettingsService settingsService,
        IAuditService auditService,
        INotificationService notificationService)
    {
        _scopeService = scopeService;
        _settingsService = settingsService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public List<Student> Children { get; set; } = new();

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public int SelectedStudentId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public DateTime EndDate { get; set; } = DateTime.Today;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string Reason { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int? childId = null)
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return RedirectToPage("/Parent/Index");

        Children = await Db.StudentGuardians
            .Where(sg => sg.GuardianId == guardianId.Value)
            .Include(sg => sg.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Select(sg => sg.Student)
            .ToListAsync();

        if (childId.HasValue && Children.Any(c => c.Id == childId.Value))
        {
            SelectedStudentId = childId.Value;
        }
        else if (Children.Any())
        {
            SelectedStudentId = Children.First().Id;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return RedirectToPage("/Parent/Index");

        // Scope check (BR-06, BR-34)
        if (!await _scopeService.CanAccessStudentAsync(SelectedStudentId))
        {
            Toast("MSG10");
            return RedirectToPage("/Parent/Requests/Index");
        }

        if (string.IsNullOrWhiteSpace(Reason) || StartDate > EndDate)
        {
            Toast("MSG01", "Khoảng thời gian nghỉ hoặc lý do không hợp lệ.", "error");
            return await OnGetAsync(SelectedStudentId);
        }

        // UC-25 E1: Overlap check (BR-24, MSG24)
        var hasOverlap = await Db.AdministrativeRequests
            .Where(r => r.StudentId == SelectedStudentId && r.RequestType == RequestType.Leave &&
                        r.Status != RequestStatus.Cancelled && r.Status != RequestStatus.Rejected)
            .AnyAsync(r => r.LeaveRequest != null &&
                           r.LeaveRequest.StartDate <= EndDate.Date && r.LeaveRequest.EndDate >= StartDate.Date);

        if (hasOverlap)
        {
            Toast("MSG24");
            return await OnGetAsync(SelectedStudentId);
        }

        int totalDays = (EndDate.Date - StartDate.Date).Days + 1;

        // Escalation rule (BR-25, BR-41)
        var maxTeacherDays = await _settingsService.GetIntAsync("Leave.MaxDaysForTeacher", 3);
        bool isEscalated = totalDays > maxTeacherDays;

        var count = await Db.AdministrativeRequests.CountAsync() + 1;
        var requestCode = $"REQ-2026-{count:D4}";

        var request = new AdministrativeRequest
        {
            RequestCode = requestCode,
            RequestType = RequestType.Leave,
            StudentId = SelectedStudentId,
            RequesterGuardianId = guardianId.Value,
            Status = isEscalated ? RequestStatus.Escalated : RequestStatus.Submitted,
            CurrentAssigneeRole = isEscalated ? nameof(SystemRole.Board) : nameof(SystemRole.Teacher),
            SubmittedAt = DateTime.UtcNow,
            Notes = isEscalated ? $"Đơn nghỉ {totalDays} ngày (vượt quá {maxTeacherDays} ngày), đã chuyển lên Ban Giám hiệu." : null
        };

        request.LeaveRequest = new LeaveRequest
        {
            StartDate = StartDate.Date,
            EndDate = EndDate.Date,
            TotalDays = totalDays,
            Reason = Reason.Trim(),
            IsEscalatedToBoard = isEscalated
        };

        request.StatusHistories.Add(new RequestStatusHistory
        {
            FromStatus = RequestStatus.Draft,
            ToStatus = request.Status,
            ChangedByUserId = CurrentUser.UserId,
            ChangedAt = DateTime.UtcNow,
            ActionNotes = "Phụ huynh gửi đơn xin nghỉ học qua hệ thống"
        });

        Db.AdministrativeRequests.Add(request);
        await Db.SaveChangesAsync();

        // Audit log (BR-11, BR-45)
        await _auditService.LogAsync(
            "SubmitLeave",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Nộp đơn xin nghỉ học {requestCode} ({totalDays} ngày) cho học sinh Id={SelectedStudentId} (Escalated={isEscalated})");

        // Notify assignee role (BR-42)
        var student = await Db.Students.FindAsync(SelectedStudentId);
        if (isEscalated)
        {
            await _notificationService.NotifyRoleAsync(
                nameof(SystemRole.Board),
                "Đơn xin nghỉ học dài ngày cần phê duyệt (BGH)",
                $"Học sinh {student?.FullName} có đơn xin nghỉ {totalDays} ngày (vượt hạn mức GVCN). Mã: {requestCode}",
                "/Board/Requests/Leave");
        }
        else
        {
            await _notificationService.NotifyRoleAsync(
                nameof(SystemRole.Teacher),
                "Đơn xin nghỉ học mới cần duyệt (GVCN)",
                $"Học sinh {student?.FullName} có đơn xin nghỉ {totalDays} ngày từ {FormatDate(StartDate)}. Mã: {requestCode}",
                "/Teacher/Leave/Index");
        }

        Toast("MSG21");
        return RedirectToPage("/Parent/Requests/Index");
    }
}
