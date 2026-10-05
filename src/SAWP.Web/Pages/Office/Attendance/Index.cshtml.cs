using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Attendance;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public IndexModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public int? ClassId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    public List<SchoolClass> AvailableClasses { get; set; } = new();
    public SchoolClass? SelectedClass { get; set; }
    public List<AttendanceRecordItem> Records { get; set; } = new();

    // Correction Form
    [BindProperty]
    public int StudentId { get; set; }

    [BindProperty]
    public AttendanceStatus NewStatus { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string CorrectionReason { get; set; } = string.Empty;

    public class AttendanceRecordItem
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
        public string? Note { get; set; }
        public string MarkedBy { get; set; } = "Chưa ghi";
        public string? CorrectedBy { get; set; }
        public string? CorrectionReason { get; set; }
    }

    public async Task OnGetAsync()
    {
        AvailableClasses = await Db.SchoolClasses
            .Where(c => c.IsActive)
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.Name)
            .ToListAsync();

        SelectedClass = ClassId.HasValue
            ? AvailableClasses.FirstOrDefault(c => c.Id == ClassId.Value)
            : AvailableClasses.FirstOrDefault();

        if (SelectedClass != null)
        {
            ClassId = SelectedClass.Id;

            var assignments = await Db.ClassAssignments
                .Where(ca => ca.ClassId == SelectedClass.Id && ca.IsActive)
                .Include(ca => ca.Student)
                .OrderBy(ca => ca.RollNumber ?? 999)
                .ThenBy(ca => ca.Student.FullName)
                .ToListAsync();

            var attRecords = await Db.AttendanceRecords
                .Where(a => a.ClassId == SelectedClass.Id && a.Date == Date.Date)
                .Include(a => a.MarkedByStaff)
                .Include(a => a.CorrectedByStaff)
                .ToDictionaryAsync(a => a.StudentId);

            Records = assignments.Select(ca =>
            {
                var s = ca.Student;
                attRecords.TryGetValue(s.Id, out var rec);

                return new AttendanceRecordItem
                {
                    StudentId = s.Id,
                    StudentCode = s.StudentCode,
                    FullName = s.FullName,
                    Status = rec?.Status ?? AttendanceStatus.Present,
                    Note = rec?.Note,
                    MarkedBy = rec?.MarkedByStaff?.FullName ?? "Chưa ghi nhận",
                    CorrectedBy = rec?.CorrectedByStaff?.FullName,
                    CorrectionReason = rec?.CorrectionReason
                };
            }).ToList();
        }
    }

    public async Task<IActionResult> OnPostCorrectAsync()
    {
        if (string.IsNullOrWhiteSpace(CorrectionReason))
        {
            Toast("MSG01", "Lý do điều chỉnh điểm danh là bắt buộc (BR-22).", "error");
            await OnGetAsync();
            return Page();
        }

        var staffId = await CurrentUser.GetStaffProfileIdAsync();

        var record = await Db.AttendanceRecords
            .Include(a => a.Student)
            .Include(a => a.Class)
            .FirstOrDefaultAsync(a => a.StudentId == StudentId && a.Date == Date.Date);

        if (record == null)
        {
            record = new AttendanceRecord
            {
                StudentId = StudentId,
                ClassId = ClassId ?? 1,
                Date = Date.Date,
                Status = NewStatus,
                CorrectedByStaffId = staffId,
                CorrectedAt = DateTime.UtcNow,
                CorrectionReason = CorrectionReason.Trim()
            };
            Db.AttendanceRecords.Add(record);
        }
        else
        {
            var oldStatus = record.Status;
            record.Status = NewStatus;
            record.CorrectedByStaffId = staffId;
            record.CorrectedAt = DateTime.UtcNow;
            record.CorrectionReason = CorrectionReason.Trim();

            await _auditService.LogAsync(
                "CorrectAttendance",
                nameof(AttendanceRecord),
                record.Id.ToString(),
                $"Giáo vụ điều chỉnh điểm danh học sinh {record.Student.FullName} ngày {FormatDate(Date)} từ {oldStatus} sang {NewStatus}. Lý do: {CorrectionReason} (UC-22, BR-22)");
        }

        await Db.SaveChangesAsync();

        Toast("MSG18", "Đã điều chỉnh bản ghi điểm danh và lưu vết kiểm toán thành công.", "success");
        return RedirectToPage(new { ClassId, Date = Date.ToString("yyyy-MM-dd") });
    }
}
