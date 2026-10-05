using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Service.Lunch;

public class DailyModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public DailyModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;

    [BindProperty(SupportsGet = true)]
    public int? ClassId { get; set; }

    public LunchServiceDay? ServiceDay { get; set; }
    public bool IsPastCutoff { get; set; }
    public int TotalMeals { get; set; }
    public List<ClassMealCountDto> ClassCounts { get; set; } = new();
    public List<SchoolClass> AvailableClasses { get; set; } = new();
    public List<StudentMealItemViewModel> StudentMeals { get; set; } = new();

    public class ClassMealCountDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int RegisteredCount { get; set; }
        public int TotalStudents { get; set; }
    }

    public class StudentMealItemViewModel
    {
        public int RegistrationId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public LunchRegistrationStatus Status { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string? AdjustmentReason { get; set; }
        public string? AdjustedByStaffName { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Date == default) Date = Clock.Today;

        ServiceDay = await Db.LunchServiceDays
            .FirstOrDefaultAsync(d => d.Date == Date.Date);

        var cutoffTime = ServiceDay?.CutoffTime ?? new TimeSpan(8, 0, 0);
        IsPastCutoff = Clock.LocalNow > Date.Date.Add(cutoffTime);

        AvailableClasses = await Db.SchoolClasses
            .Where(c => c.IsActive)
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.Name)
            .ToListAsync();

        // Compute class counts
        var activeAssignments = await Db.ClassAssignments
            .Where(ca => ca.IsActive)
            .Include(ca => ca.Class)
            .Include(ca => ca.Student)
            .ToListAsync();

        var registrationsQuery = Db.LunchRegistrations
            .Where(r => r.Date == Date.Date)
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Include(r => r.AdjustedByStaff)
            .AsQueryable();

        var allRegistrations = await registrationsQuery.ToListAsync();

        TotalMeals = allRegistrations.Count(r => r.Status == LunchRegistrationStatus.Registered);

        foreach (var cls in AvailableClasses)
        {
            var studentsInClass = activeAssignments.Where(ca => ca.ClassId == cls.Id).Select(ca => ca.StudentId).ToList();
            var count = allRegistrations.Count(r => studentsInClass.Contains(r.StudentId) && r.Status == LunchRegistrationStatus.Registered);

            ClassCounts.Add(new ClassMealCountDto
            {
                ClassId = cls.Id,
                ClassName = cls.Name,
                RegisteredCount = count,
                TotalStudents = studentsInClass.Count
            });
        }

        var filteredRegs = allRegistrations.AsEnumerable();
        if (ClassId.HasValue)
        {
            var studentsInFilter = activeAssignments.Where(ca => ca.ClassId == ClassId.Value).Select(ca => ca.StudentId).ToList();
            filteredRegs = filteredRegs.Where(r => studentsInFilter.Contains(r.StudentId));
        }

        StudentMeals = filteredRegs
            .Select(r => new StudentMealItemViewModel
            {
                RegistrationId = r.Id,
                StudentId = r.StudentId,
                StudentCode = r.Student?.StudentCode ?? "",
                FullName = r.Student?.FullName ?? "",
                ClassName = r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.Name ?? "—",
                Status = r.Status,
                RegisteredAt = r.RegisteredAt,
                AdjustmentReason = r.AdjustmentReason,
                AdjustedByStaffName = r.AdjustedByStaff?.FullName
            })
            .OrderBy(m => m.ClassName)
            .ThenBy(m => m.FullName)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAdjustMealAsync(int studentId, DateTime mealDate, string actionType, string reason)
    {
        // UC-34: Staff lunch adjustment after parent cutoff (BR-31)
        if (string.IsNullOrWhiteSpace(reason))
        {
            Toast("MSG01", "Bắt buộc phải nhập lý do điều chỉnh suất ăn (BR-31).", "error");
            return RedirectToPage(new { Date = mealDate, ClassId });
        }

        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        var reg = await Db.LunchRegistrations
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date == mealDate.Date);

        if (actionType == "Register")
        {
            if (reg == null)
            {
                var sDay = await Db.LunchServiceDays.FirstOrDefaultAsync(d => d.Date == mealDate.Date);
                reg = new LunchRegistration
                {
                    StudentId = studentId,
                    LunchServiceDayId = sDay?.Id ?? 1,
                    Date = mealDate.Date,
                    Status = LunchRegistrationStatus.Registered,
                    RegisteredAt = DateTime.UtcNow,
                    AdjustedByStaffId = staffId,
                    AdjustmentReason = $"[Nhân viên bổ sung] {reason.Trim()}"
                };
                Db.LunchRegistrations.Add(reg);
            }
            else
            {
                reg.Status = LunchRegistrationStatus.Registered;
                reg.AdjustedByStaffId = staffId;
                reg.AdjustmentReason = $"[Nhân viên kích hoạt lại] {reason.Trim()}";
                reg.CancelledAt = null;
            }

            await Db.SaveChangesAsync();
            await _auditService.LogAsync(
                "StaffAdjustLunchAdd",
                nameof(LunchRegistration),
                reg.Id.ToString(),
                $"Nhân viên bếp bổ sung suất ăn cho học sinh Id={studentId} ngày {FormatDate(mealDate)}. Lý do: {reason.Trim()}");

            Toast("MSG08", "Đã bổ sung suất ăn thành công.", "success");
        }
        else if (actionType == "Cancel")
        {
            if (reg != null)
            {
                reg.Status = LunchRegistrationStatus.Cancelled;
                reg.CancelledAt = DateTime.UtcNow;
                reg.AdjustedByStaffId = staffId;
                reg.AdjustmentReason = $"[Nhân viên hủy] {reason.Trim()}";

                await Db.SaveChangesAsync();
                await _auditService.LogAsync(
                    "StaffAdjustLunchCancel",
                    nameof(LunchRegistration),
                    reg.Id.ToString(),
                    $"Nhân viên bếp hủy suất ăn cho học sinh Id={studentId} ngày {FormatDate(mealDate)}. Lý do: {reason.Trim()}");

                Toast("MSG08", "Đã hủy suất ăn thành công.", "success");
            }
        }

        return RedirectToPage(new { Date = mealDate, ClassId });
    }
}
