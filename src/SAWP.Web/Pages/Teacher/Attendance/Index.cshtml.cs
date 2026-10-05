using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Teacher.Attendance;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;
    private readonly ISettingsService _settingsService;

    public IndexModel(IAuditService auditService, ISettingsService settingsService)
    {
        _auditService = auditService;
        _settingsService = settingsService;
    }

    public SchoolClass? AssignedClass { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    public bool IsLocked { get; set; }
    public List<StudentAttendanceItemViewModel> StudentRows { get; set; } = new();

    [BindProperty]
    public List<AttendanceInputModel> Items { get; set; } = new();

    public class AttendanceInputModel
    {
        public int StudentId { get; set; }
        public AttendanceStatus Status { get; set; }
        public string? Note { get; set; }
    }

    public class StudentAttendanceItemViewModel
    {
        public int StudentId { get; set; }
        public int? RollNumber { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public AttendanceStatus CurrentStatus { get; set; } = AttendanceStatus.Present;
        public string? CurrentNote { get; set; }
        public string? LeaveIndicator { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return RedirectToPage("/Teacher/Index");

        var hr = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        // UC-21 E2: Teacher is not assigned to class (BR-21, MSG20)
        if (hr == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        AssignedClass = hr.Class;

        // Check lock window (UC-22, BR-22)
        var windowHours = await _settingsService.GetIntAsync("Attendance.TeacherEditWindowHours", 24);
        var targetDateTime = Date.Date.AddDays(1); // end of attendance date
        IsLocked = Clock.LocalNow > targetDateTime.AddHours(windowHours);

        var assignments = await Db.ClassAssignments
            .Where(ca => ca.ClassId == AssignedClass.Id && ca.IsActive)
            .Include(ca => ca.Student)
            .OrderBy(ca => ca.RollNumber ?? 999)
            .ThenBy(ca => ca.Student.FullName)
            .ToListAsync();

        var existingRecords = await Db.AttendanceRecords
            .Where(a => a.ClassId == AssignedClass.Id && a.Date == Date.Date)
            .ToDictionaryAsync(a => a.StudentId);

        // Check approved/pending leave requests (BR-26)
        var leaveRequests = await Db.AdministrativeRequests
            .Where(r => r.RequestType == RequestType.Leave && 
                        (r.Status == RequestStatus.Approved || r.Status == RequestStatus.Submitted) &&
                        r.LeaveRequest != null && r.LeaveRequest.StartDate <= Date.Date && r.LeaveRequest.EndDate >= Date.Date)
            .Include(r => r.LeaveRequest)
            .ToDictionaryAsync(r => r.StudentId);

        StudentRows = assignments.Select(ca =>
        {
            var s = ca.Student;
            existingRecords.TryGetValue(s.Id, out var record);
            leaveRequests.TryGetValue(s.Id, out var leave);

            var leaveInd = leave != null
                ? (leave.Status == RequestStatus.Approved ? "Đã duyệt nghỉ phép (BR-26)" : "Có đơn xin nghỉ chờ duyệt")
                : null;

            return new StudentAttendanceItemViewModel
            {
                StudentId = s.Id,
                RollNumber = ca.RollNumber,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                CurrentStatus = record?.Status ?? (leave?.Status == RequestStatus.Approved ? AttendanceStatus.AbsentWithLeave : AttendanceStatus.Present),
                CurrentNote = record?.Note,
                LeaveIndicator = leaveInd
            };
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return RedirectToPage("/Teacher/Index");

        var hr = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (hr == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        // Check lock window (UC-22 E1: MSG19)
        var windowHours = await _settingsService.GetIntAsync("Attendance.TeacherEditWindowHours", 24);
        var targetDateTime = Date.Date.AddDays(1);
        if (Clock.LocalNow > targetDateTime.AddHours(windowHours))
        {
            Toast("MSG19");
            return await OnGetAsync();
        }

        foreach (var item in Items)
        {
            var record = await Db.AttendanceRecords
                .FirstOrDefaultAsync(a => a.StudentId == item.StudentId && a.Date == Date.Date);

            if (record == null)
            {
                record = new AttendanceRecord
                {
                    StudentId = item.StudentId,
                    ClassId = hr.ClassId,
                    Date = Date.Date,
                    Status = item.Status,
                    Note = item.Note?.Trim(),
                    MarkedByStaffId = staffId.Value,
                    MarkedAt = DateTime.UtcNow
                };
                Db.AttendanceRecords.Add(record);
            }
            else
            {
                record.Status = item.Status;
                record.Note = item.Note?.Trim();
                record.MarkedByStaffId = staffId.Value;
                record.MarkedAt = DateTime.UtcNow;
            }
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "RecordAttendance",
            nameof(AttendanceRecord),
            hr.ClassId.ToString(),
            $"Điểm danh Lớp {hr.Class.Name} ngày {FormatDate(Date)} ({Items.Count} học sinh) (UC-21)");

        Toast("MSG18");
        return RedirectToPage(new { Date = Date.ToString("yyyy-MM-dd") });
    }
}
