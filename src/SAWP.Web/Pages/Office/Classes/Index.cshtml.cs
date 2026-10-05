using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Classes;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public IndexModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public List<SchoolYear> SchoolYears { get; set; } = new();
    public SchoolYear? ActiveSchoolYear { get; set; }
    public List<ClassItemViewModel> Classes { get; set; } = new();

    // Form: Create Class (UC-14)
    [BindProperty]
    public string ClassName { get; set; } = string.Empty;

    [BindProperty]
    public int Grade { get; set; } = 1;

    [BindProperty]
    public string? RoomNumber { get; set; }

    [BindProperty]
    public int Capacity { get; set; } = 35;

    // Form: Create School Year (UC-13)
    [BindProperty]
    public string YearCode { get; set; } = string.Empty;

    [BindProperty]
    public string YearName { get; set; } = string.Empty;

    [BindProperty]
    public DateTime StartDate { get; set; } = new DateTime(2026, 9, 1);

    [BindProperty]
    public DateTime EndDate { get; set; } = new DateTime(2027, 5, 31);

    public class ClassItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Grade { get; set; }
        public string? RoomNumber { get; set; }
        public int Capacity { get; set; }
        public int StudentCount { get; set; }
        public string HomeroomTeacherName { get; set; } = "Chưa phân công";
        public bool IsActive { get; set; }
    }

    public async Task OnGetAsync(int? yearId = null)
    {
        SchoolYears = await Db.SchoolYears.OrderByDescending(y => y.StartDate).ToListAsync();
        
        ActiveSchoolYear = yearId.HasValue 
            ? SchoolYears.FirstOrDefault(y => y.Id == yearId.Value) 
            : SchoolYears.FirstOrDefault(y => y.Status == SchoolYearStatus.Active) ?? SchoolYears.FirstOrDefault();

        if (ActiveSchoolYear != null)
        {
            var rawClasses = await Db.SchoolClasses
                .Where(c => c.SchoolYearId == ActiveSchoolYear.Id)
                .Include(c => c.ClassAssignments.Where(ca => ca.IsActive))
                .Include(c => c.HomeroomAssignments.Where(ha => ha.IsActive))
                .ThenInclude(ha => ha.StaffProfile)
                .OrderBy(c => c.Grade)
                .ThenBy(c => c.Name)
                .ToListAsync();

            Classes = rawClasses.Select(c => new ClassItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Grade = c.Grade,
                RoomNumber = c.RoomNumber,
                Capacity = c.Capacity,
                StudentCount = c.ClassAssignments.Count,
                HomeroomTeacherName = c.HomeroomAssignments.FirstOrDefault()?.StaffProfile?.FullName ?? "Chưa phân công",
                IsActive = c.IsActive
            }).ToList();
        }
    }

    public async Task<IActionResult> OnPostCreateClassAsync(int yearId)
    {
        if (string.IsNullOrWhiteSpace(ClassName) || Grade < 1 || Grade > 5 || Capacity <= 0)
        {
            Toast("MSG01");
            await OnGetAsync(yearId);
            return Page();
        }

        var year = await Db.SchoolYears.FindAsync(yearId);
        if (year == null)
        {
            Toast("MSG14");
            return RedirectToPage();
        }

        // Check duplicate name in same school year
        var exists = await Db.SchoolClasses.AnyAsync(c => c.SchoolYearId == yearId && c.Name == ClassName.Trim());
        if (exists)
        {
            Toast("MSG01", "Tên lớp học đã tồn tại trong năm học này.", "error");
            await OnGetAsync(yearId);
            return Page();
        }

        var cls = new SchoolClass
        {
            SchoolYearId = yearId,
            Name = ClassName.Trim(),
            Grade = Grade,
            RoomNumber = RoomNumber?.Trim(),
            Capacity = Capacity,
            IsActive = true
        };

        Db.SchoolClasses.Add(cls);
        await Db.SaveChangesAsync();

        await _auditService.LogAsync("CreateClass", nameof(SchoolClass), cls.Id.ToString(), $"Tạo lớp {cls.Name} (Khối {cls.Grade}, Sĩ số {cls.Capacity})");
        Toast("MSG08");

        return RedirectToPage(new { yearId });
    }

    public async Task<IActionResult> OnPostCreateYearAsync()
    {
        if (string.IsNullOrWhiteSpace(YearCode) || string.IsNullOrWhiteSpace(YearName) || StartDate >= EndDate)
        {
            Toast("MSG01", "Thông tin năm học không hợp lệ hoặc ngày bắt đầu phải trước ngày kết thúc.", "error");
            return RedirectToPage();
        }

        var sy = new SchoolYear
        {
            Code = YearCode.Trim(),
            Name = YearName.Trim(),
            StartDate = StartDate,
            EndDate = EndDate,
            Status = SchoolYearStatus.Planned
        };

        Db.SchoolYears.Add(sy);
        await Db.SaveChangesAsync();

        await _auditService.LogAsync("CreateSchoolYear", nameof(SchoolYear), sy.Id.ToString(), $"Tạo năm học {sy.Name} ({sy.Code})");
        Toast("MSG08");

        return RedirectToPage(new { yearId = sy.Id });
    }
}
