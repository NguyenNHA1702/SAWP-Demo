using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Parent.Requests;

public class CreateTransferModel : AppPageModel
{
    private readonly IScopeService _scopeService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public CreateTransferModel(
        IScopeService scopeService,
        IAuditService auditService,
        INotificationService notificationService)
    {
        _scopeService = scopeService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public List<Student> Children { get; set; } = new();
    public List<SchoolClass> EligibleTargetClasses { get; set; } = new();

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public int SelectedStudentId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public int TargetClassId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    [MinLength(10, ErrorMessage = "Vui lòng nhập lý do cụ thể (tối thiểu 10 ký tự).")]
    public string Reason { get; set; } = string.Empty;

    public Student? CurrentStudent { get; set; }
    public SchoolClass? CurrentClass { get; set; }

    public async Task<IActionResult> OnGetAsync(int? studentId = null)
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

        if (!Children.Any()) return RedirectToPage("/Parent/Requests/Index");

        if (studentId.HasValue && Children.Any(c => c.Id == studentId.Value))
        {
            SelectedStudentId = studentId.Value;
        }
        else
        {
            SelectedStudentId = Children.First().Id;
        }

        CurrentStudent = Children.First(c => c.Id == SelectedStudentId);
        CurrentClass = CurrentStudent.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class;

        if (CurrentClass != null)
        {
            // Target classes must be in the same grade, active, and not the current class (BR-14, BR-15)
            var currentGrade = CurrentClass.Grade;
            var classesInGrade = await Db.SchoolClasses
                .Where(c => c.Grade == currentGrade && c.IsActive && c.Id != CurrentClass.Id)
                .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
                .ToListAsync();

            EligibleTargetClasses = classesInGrade;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return RedirectToPage("/Parent/Index");

        if (!await _scopeService.CanAccessStudentAsync(SelectedStudentId))
        {
            Toast("MSG10");
            return RedirectToPage("/Parent/Requests/Index");
        }

        var student = await Db.Students
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .FirstOrDefaultAsync(s => s.Id == SelectedStudentId);

        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Parent/Requests/Index");
        }

        var currentAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
        if (currentAssignment == null)
        {
            Toast("MSG09", "Học sinh hiện chưa được phân lớp chính thức.", "error");
            return await OnGetAsync(SelectedStudentId);
        }

        var targetClass = await Db.SchoolClasses
            .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
            .FirstOrDefaultAsync(c => c.Id == TargetClassId && c.IsActive);

        if (targetClass == null || targetClass.Id == currentAssignment.ClassId)
        {
            Toast("MSG01", "Lớp chuyển đến không hợp lệ.", "error");
            return await OnGetAsync(SelectedStudentId);
        }

        // Grade match check
        if (targetClass.Grade != currentAssignment.Class.Grade)
        {
            Toast("MSG01", "Chỉ được chuyển lớp trong cùng khối lớp.", "error");
            return await OnGetAsync(SelectedStudentId);
        }

        // Capacity check (BR-15, MSG15)
        var currentActiveInTarget = targetClass.ClassAssignments.Count;
        if (currentActiveInTarget >= targetClass.Capacity)
        {
            Toast("MSG15");
            return await OnGetAsync(SelectedStudentId);
        }

        // Check if pending transfer request already exists (UC-36 E1)
        var hasPendingTransfer = await Db.AdministrativeRequests
            .AnyAsync(r => r.StudentId == SelectedStudentId &&
                           r.RequestType == RequestType.ClassTransfer &&
                           (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.InReview || r.Status == RequestStatus.Escalated));

        if (hasPendingTransfer)
        {
            Toast("MSG09", "Học sinh đã có một đơn xin chuyển lớp đang trong quá trình xét duyệt.", "warning");
            return await OnGetAsync(SelectedStudentId);
        }

        var count = await Db.AdministrativeRequests.CountAsync() + 1;
        var requestCode = $"REQ-TR-{count:D4}";

        var request = new AdministrativeRequest
        {
            RequestCode = requestCode,
            RequestType = RequestType.ClassTransfer,
            StudentId = SelectedStudentId,
            RequesterGuardianId = guardianId.Value,
            Status = RequestStatus.Submitted,
            CurrentAssigneeRole = nameof(SystemRole.Office),
            SubmittedAt = DateTime.UtcNow,
            Notes = $"Đề nghị chuyển từ lớp {currentAssignment.Class.Name} sang {targetClass.Name}."
        };

        request.ClassTransferDetail = new ClassTransferDetail
        {
            CurrentClassId = currentAssignment.ClassId,
            TargetClassId = targetClass.Id,
            Reason = Reason.Trim(),
            IsApplied = false
        };

        request.StatusHistories.Add(new RequestStatusHistory
        {
            FromStatus = RequestStatus.Draft,
            ToStatus = RequestStatus.Submitted,
            ChangedByUserId = CurrentUser.UserId,
            ChangedAt = DateTime.UtcNow,
            ActionNotes = "Phụ huynh gửi đơn xin chuyển lớp qua cổng trực tuyến"
        });

        Db.AdministrativeRequests.Add(request);
        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "SubmitTransferRequest",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Phụ huynh gửi đơn chuyển lớp {requestCode} cho học sinh {student.FullName}: {currentAssignment.Class.Name} -> {targetClass.Name}");

        await _notificationService.NotifyRoleAsync(
            nameof(SystemRole.Office),
            "Đơn xin chuyển lớp mới cần rà soát",
            $"Học sinh {student.FullName} xin chuyển từ lớp {currentAssignment.Class.Name} sang {targetClass.Name}. Mã: {requestCode}",
            "/Office/Requests/Transfers");

        Toast("MSG30");
        return RedirectToPage("/Parent/Requests/Index");
    }
}
