using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Parent.Lunch;

public class IndexModel : AppPageModel
{
    private readonly IScopeService _scopeService;
    private readonly IAuditService _auditService;

    public IndexModel(IScopeService scopeService, IAuditService auditService)
    {
        _scopeService = scopeService;
        _auditService = auditService;
    }

    public List<Student> Children { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ChildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; }

    public Student? SelectedChild { get; set; }
    public List<DayLunchItemViewModel> DayItems { get; set; } = new();
    public int TotalRegisteredMeals { get; set; }
    public decimal TotalEstimatedFee { get; set; }

    public class DayLunchItemViewModel
    {
        public int LunchServiceDayId { get; set; }
        public DateTime Date { get; set; }
        public bool IsAvailable { get; set; }
        public TimeSpan CutoffTime { get; set; }
        public string? MenuDescription { get; set; }
        public decimal Price { get; set; }
        public bool IsPastCutoff { get; set; }
        public int? RegistrationId { get; set; }
        public bool IsRegistered { get; set; }
        public bool IsCancelled { get; set; }
        public string? Note { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
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

        if (!Children.Any()) return Page();

        if (ChildId.HasValue && Children.Any(c => c.Id == ChildId.Value))
        {
            SelectedChild = Children.First(c => c.Id == ChildId.Value);
        }
        else
        {
            SelectedChild = Children.First();
            ChildId = SelectedChild.Id;
        }

        var now = Clock.LocalNow;
        if (Month < 1 || Month > 12) Month = now.Month;
        if (Year < 2020 || Year > 2030) Year = now.Year;

        var startDate = new DateTime(Year, Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Fetch service days for this month
        var serviceDays = await Db.LunchServiceDays
            .Where(d => d.Date >= startDate && d.Date <= endDate)
            .OrderBy(d => d.Date)
            .ToListAsync();

        // If no service days configured yet for this month, seed weekdays automatically for convenience
        if (!serviceDays.Any())
        {
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                {
                    var day = new LunchServiceDay
                    {
                        Date = d,
                        IsAvailable = true,
                        CutoffTime = new TimeSpan(8, 0, 0),
                        MenuDescription = "Cơm trắng, Thịt lợn kho trứng cút, Canh rau cải thịt băm, Tráng miệng chuối",
                        Price = 35000
                    };
                    Db.LunchServiceDays.Add(day);
                    serviceDays.Add(day);
                }
            }
            await Db.SaveChangesAsync();
        }

        // Fetch registrations for selected student
        var registrations = await Db.LunchRegistrations
            .Where(r => r.StudentId == SelectedChild.Id && r.Date >= startDate && r.Date <= endDate)
            .ToListAsync();

        foreach (var day in serviceDays)
        {
            var cutoffDateTime = day.Date.Date.Add(day.CutoffTime);
            bool isPastCutoff = now > cutoffDateTime;

            var reg = registrations.FirstOrDefault(r => r.LunchServiceDayId == day.Id || r.Date.Date == day.Date.Date);
            bool isReg = reg != null && reg.Status == LunchRegistrationStatus.Registered;
            bool isCanc = reg != null && reg.Status == LunchRegistrationStatus.Cancelled;

            if (isReg)
            {
                TotalRegisteredMeals++;
                TotalEstimatedFee += day.Price;
            }

            DayItems.Add(new DayLunchItemViewModel
            {
                LunchServiceDayId = day.Id,
                Date = day.Date,
                IsAvailable = day.IsAvailable,
                CutoffTime = day.CutoffTime,
                MenuDescription = day.MenuDescription,
                Price = day.Price,
                IsPastCutoff = isPastCutoff,
                RegistrationId = reg?.Id,
                IsRegistered = isReg,
                IsCancelled = isCanc,
                Note = reg?.AdjustmentReason
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync(int studentId, int lunchServiceDayId)
    {
        // Scope check (BR-06)
        if (!await _scopeService.CanAccessStudentAsync(studentId))
        {
            Toast("MSG10");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        var serviceDay = await Db.LunchServiceDays.FindAsync(lunchServiceDayId);
        if (serviceDay == null || !serviceDay.IsAvailable)
        {
            Toast("MSG28");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        // Cutoff check (BR-28, MSG27)
        var cutoffDateTime = serviceDay.Date.Date.Add(serviceDay.CutoffTime);
        if (Clock.LocalNow > cutoffDateTime)
        {
            Toast("MSG27");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        // Check duplicate active (BR-30, MSG29)
        var reg = await Db.LunchRegistrations
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Date.Date == serviceDay.Date.Date);

        if (reg != null && reg.Status == LunchRegistrationStatus.Registered)
        {
            Toast("MSG29");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        if (reg != null)
        {
            reg.Status = LunchRegistrationStatus.Registered;
            reg.RegisteredAt = DateTime.UtcNow;
            reg.CancelledAt = null;
            reg.AdjustmentReason = null;
        }
        else
        {
            reg = new LunchRegistration
            {
                StudentId = studentId,
                LunchServiceDayId = serviceDay.Id,
                Date = serviceDay.Date.Date,
                Status = LunchRegistrationStatus.Registered,
                RegisteredAt = DateTime.UtcNow
            };
            Db.LunchRegistrations.Add(reg);
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "RegisterLunch",
            nameof(LunchRegistration),
            reg.Id.ToString(),
            $"Phụ huynh đăng ký ăn trưa ngày {FormatDate(serviceDay.Date)} cho học sinh Id={studentId}");

        Toast("MSG25");
        return RedirectToPage(new { ChildId = studentId, Month, Year });
    }

    public async Task<IActionResult> OnPostCancelAsync(int studentId, int registrationId)
    {
        // Scope check (BR-06)
        if (!await _scopeService.CanAccessStudentAsync(studentId))
        {
            Toast("MSG10");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        var reg = await Db.LunchRegistrations
            .Include(r => r.LunchServiceDay)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.StudentId == studentId);

        if (reg == null)
        {
            Toast("MSG09");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        // Cutoff check (BR-28, MSG27)
        var cutoff = reg.LunchServiceDay?.CutoffTime ?? new TimeSpan(8, 0, 0);
        var cutoffDateTime = reg.Date.Date.Add(cutoff);
        if (Clock.LocalNow > cutoffDateTime)
        {
            Toast("MSG27");
            return RedirectToPage(new { ChildId = studentId, Month, Year });
        }

        reg.Status = LunchRegistrationStatus.Cancelled;
        reg.CancelledAt = DateTime.UtcNow;
        reg.AdjustmentReason = "Phụ huynh tự hủy trước hạn chốt";

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "CancelLunch",
            nameof(LunchRegistration),
            reg.Id.ToString(),
            $"Phụ huynh hủy đăng ký ăn trưa ngày {FormatDate(reg.Date)} cho học sinh Id={studentId}");

        Toast("MSG26");
        return RedirectToPage(new { ChildId = studentId, Month, Year });
    }
}
