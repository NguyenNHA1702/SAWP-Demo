using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Parent.Requests;

public class CreateDocumentModel : AppPageModel
{
    private readonly IScopeService _scopeService;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public CreateDocumentModel(
        IScopeService scopeService,
        IAuditService auditService,
        INotificationService notificationService)
    {
        _scopeService = scopeService;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public List<Student> Children { get; set; } = new();

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public int SelectedStudentId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string DocumentType { get; set; } = "Giấy xác nhận đang học tại trường";

    [BindProperty]
    [Range(1, 5, ErrorMessage = "Số lượng bản cấp từ 1 đến 5 bản.")]
    public int Quantity { get; set; } = 1;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    [MinLength(5, ErrorMessage = "Vui lòng ghi rõ mục đích sử dụng giấy tờ.")]
    public string Purpose { get; set; } = string.Empty;

    [BindProperty]
    public string? Notes { get; set; }

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

        if (string.IsNullOrWhiteSpace(Purpose))
        {
            Toast("MSG01", "Vui lòng nhập mục đích xin cấp giấy tờ.", "error");
            return await OnGetAsync(SelectedStudentId);
        }

        var student = await Db.Students.FindAsync(SelectedStudentId);
        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Parent/Requests/Index");
        }

        var count = await Db.AdministrativeRequests.CountAsync() + 1;
        var requestCode = $"REQ-DOC-{count:D4}";

        var request = new AdministrativeRequest
        {
            RequestCode = requestCode,
            RequestType = RequestType.DocumentIssuance,
            StudentId = SelectedStudentId,
            RequesterGuardianId = guardianId.Value,
            Status = RequestStatus.Submitted,
            CurrentAssigneeRole = nameof(SystemRole.Office),
            SubmittedAt = DateTime.UtcNow,
            Notes = $"Xin cấp: {DocumentType} (Số lượng: {Quantity} bản). Mục đích: {Purpose}"
        };

        request.DocumentRequestDetail = new DocumentRequestDetail
        {
            DocumentType = DocumentType.Trim(),
            Quantity = Quantity,
            Purpose = Purpose.Trim(),
            Notes = Notes?.Trim()
        };

        request.StatusHistories.Add(new RequestStatusHistory
        {
            FromStatus = RequestStatus.Draft,
            ToStatus = RequestStatus.Submitted,
            ChangedByUserId = CurrentUser.UserId,
            ChangedAt = DateTime.UtcNow,
            ActionNotes = "Phụ huynh gửi đề nghị cấp giấy xác nhận trực tuyến"
        });

        Db.AdministrativeRequests.Add(request);
        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "SubmitDocumentRequest",
            nameof(AdministrativeRequest),
            request.Id.ToString(),
            $"Phụ huynh yêu cầu cấp {DocumentType} ({Quantity} bản) cho học sinh {student.FullName}. Mã: {requestCode}");

        await _notificationService.NotifyRoleAsync(
            nameof(SystemRole.Office),
            "Yêu cầu cấp giấy tờ/bản sao mới",
            $"Phụ huynh học sinh {student.FullName} gửi đề nghị cấp '{DocumentType}'. Mã: {requestCode}",
            "/Office/Requests/Documents");

        Toast("MSG30");
        return RedirectToPage("/Parent/Requests/Index");
    }
}
