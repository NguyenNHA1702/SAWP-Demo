using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Calendar;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public IndexModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public SchoolYear? ActiveSchoolYear { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.Today.Month;

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.Today.Year;

    public List<SchoolCalendarDay> CalendarDays { get; set; } = new();

    // Form: Edit / Set Day
    [BindProperty]
    public DateTime SelectedDate { get; set; }

    [BindProperty]
    public CalendarDayType DayType { get; set; } = CalendarDayType.SchoolDay;

    [BindProperty]
    public string? Note { get; set; }

    public async Task OnGetAsync()
    {
        ActiveSchoolYear = await Db.SchoolYears.FirstOrDefaultAsync(y => y.Status == SchoolYearStatus.Active);
        if (ActiveSchoolYear == null)
        {
            Toast("MSG14");
            return;
        }

        var startDate = new DateTime(Year, Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        CalendarDays = await Db.SchoolCalendarDays
            .Where(d => d.SchoolYearId == ActiveSchoolYear.Id && d.Date >= startDate && d.Date <= endDate)
            .OrderBy(d => d.Date)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostSetDayAsync()
    {
        ActiveSchoolYear = await Db.SchoolYears.FirstOrDefaultAsync(y => y.Status == SchoolYearStatus.Active);
        if (ActiveSchoolYear == null)
        {
            Toast("MSG14");
            return RedirectToPage();
        }

        // UC-20 E1: Validate inside school year (BR-14, BR-53)
        if (SelectedDate < ActiveSchoolYear.StartDate || SelectedDate > ActiveSchoolYear.EndDate)
        {
            Toast("MSG01", "Ngày được chọn nằm ngoài phạm vi năm học đang hoạt động.", "error");
            return RedirectToPage(new { Month = SelectedDate.Month, Year = SelectedDate.Year });
        }

        var day = await Db.SchoolCalendarDays
            .FirstOrDefaultAsync(d => d.SchoolYearId == ActiveSchoolYear.Id && d.Date == SelectedDate.Date);

        if (day == null)
        {
            day = new SchoolCalendarDay
            {
                SchoolYearId = ActiveSchoolYear.Id,
                Date = SelectedDate.Date,
                DayType = DayType,
                Note = Note?.Trim()
            };
            Db.SchoolCalendarDays.Add(day);
        }
        else
        {
            day.DayType = DayType;
            day.Note = Note?.Trim();
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "ManageCalendar",
            nameof(SchoolCalendarDay),
            day.Id.ToString(),
            $"Thiết lập ngày {FormatDate(SelectedDate)} là {DayType} ({Note}) (UC-20)");

        Toast("MSG08");
        return RedirectToPage(new { Month = SelectedDate.Month, Year = SelectedDate.Year });
    }

    public async Task<IActionResult> OnPostGenerateMonthAsync(int m, int y)
    {
        ActiveSchoolYear = await Db.SchoolYears.FirstOrDefaultAsync(y => y.Status == SchoolYearStatus.Active);
        if (ActiveSchoolYear == null)
        {
            Toast("MSG14");
            return RedirectToPage();
        }

        var startDate = new DateTime(y, m, 1);
        var daysInMonth = DateTime.DaysInMonth(y, m);

        int addedCount = 0;
        for (int i = 1; i <= daysInMonth; i++)
        {
            var date = new DateTime(y, m, i);
            if (date >= ActiveSchoolYear.StartDate && date <= ActiveSchoolYear.EndDate)
            {
                var existing = await Db.SchoolCalendarDays
                    .AnyAsync(d => d.SchoolYearId == ActiveSchoolYear.Id && d.Date == date);

                if (!existing)
                {
                    var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    Db.SchoolCalendarDays.Add(new SchoolCalendarDay
                    {
                        SchoolYearId = ActiveSchoolYear.Id,
                        Date = date,
                        DayType = isWeekend ? CalendarDayType.Weekend : CalendarDayType.SchoolDay,
                        Note = isWeekend ? "Cuối tuần" : "Ngày học chuẩn"
                    });
                    addedCount++;
                }
            }
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync("GenerateCalendar", nameof(SchoolCalendarDay), null, $"Tự động sinh {addedCount} ngày học chuẩn tháng {m}/{y} (UC-20 A1)");
        Toast("MSG08", $"Đã khởi tạo lịch học tháng {m}/{y} thành công.", "success");

        return RedirectToPage(new { Month = m, Year = y });
    }
}
