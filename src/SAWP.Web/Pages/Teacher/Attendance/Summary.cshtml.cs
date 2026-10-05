using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Teacher.Attendance;

public class SummaryModel : AppPageModel
{
    private readonly IScopeService _scopeService;

    public SummaryModel(IScopeService scopeService)
    {
        _scopeService = scopeService;
    }

    public SchoolClass? HomeroomClass { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; }

    public int TotalSchoolDays { get; set; }
    public List<StudentAttendanceSummaryDto> StudentSummaries { get; set; } = new();
    public double OverallClassAttendanceRate { get; set; }
    public int TotalExcusedAbsences { get; set; }
    public int TotalUnexcusedAbsences { get; set; }

    public class StudentAttendanceSummaryDto
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int PresentDays { get; set; }
        public int ExcusedAbsenceDays { get; set; }
        public int UnexcusedAbsenceDays { get; set; }
        public int LateDays { get; set; }
        public double AttendanceRate { get; set; }
        public string Rating { get; set; } = "Tốt";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        var homeroomAssignment = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (homeroomAssignment == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        HomeroomClass = homeroomAssignment.Class;

        var now = Clock.LocalNow;
        if (Month < 1 || Month > 12) Month = now.Month;
        if (Year < 2020 || Year > 2030) Year = now.Year;

        var startDate = new DateTime(Year, Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Fetch active students in class
        var students = await Db.ClassAssignments
            .Where(ca => ca.ClassId == HomeroomClass.Id && ca.IsActive)
            .Include(ca => ca.Student)
            .Select(ca => ca.Student)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        // Fetch attendance records for this month
        var records = await Db.AttendanceRecords
            .Where(a => studentIds.Contains(a.StudentId) && a.Date >= startDate && a.Date <= endDate)
            .ToListAsync();

        // School days are distinct days where at least 1 record exists, or weekdays up to today
        var schoolDays = records.Select(a => a.Date.Date).Distinct().ToList();
        TotalSchoolDays = schoolDays.Count > 0 ? schoolDays.Count : 20;

        foreach (var student in students)
        {
            var studentRecords = records.Where(r => r.StudentId == student.Id).ToList();
            var present = studentRecords.Count(r => r.Status == AttendanceStatus.Present);
            var excused = studentRecords.Count(r => r.Status == AttendanceStatus.AbsentWithLeave);
            var unexcused = studentRecords.Count(r => r.Status == AttendanceStatus.AbsentWithoutLeave);
            var late = studentRecords.Count(r => r.Status == AttendanceStatus.Late);

            int totalRecorded = present + excused + unexcused + late;
            int divisor = totalRecorded > 0 ? totalRecorded : TotalSchoolDays;
            double rate = divisor > 0 ? ((double)(present + late) / divisor) * 100.0 : 100.0;

            string rating = "Tốt";
            if (unexcused >= 2 || rate < 85) rating = "Cần lưu ý";
            else if (rate < 95 || excused >= 3) rating = "Khá";

            StudentSummaries.Add(new StudentAttendanceSummaryDto
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                Gender = student.Gender == Gender.Male ? "Nam" : "Nữ",
                PresentDays = present,
                ExcusedAbsenceDays = excused,
                UnexcusedAbsenceDays = unexcused,
                LateDays = late,
                AttendanceRate = Math.Round(rate, 1),
                Rating = rating
            });

            TotalExcusedAbsences += excused;
            TotalUnexcusedAbsences += unexcused;
        }

        if (StudentSummaries.Count > 0)
        {
            OverallClassAttendanceRate = Math.Round(StudentSummaries.Average(s => s.AttendanceRate), 1);
        }

        return Page();
    }
}
