using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Service.Lunch;

public class ReportsModel : AppPageModel
{
    [BindProperty(SupportsGet = true)]
    public int Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; }

    public int TotalServingDays { get; set; }
    public int TotalMealsServed { get; set; }
    public decimal TotalRevenue { get; set; }
    public double AverageMealsPerDay { get; set; }

    public List<DailyReportRowDto> DailyRows { get; set; } = new();

    public class DailyReportRowDto
    {
        public DateTime Date { get; set; }
        public string? MenuDescription { get; set; }
        public int Grade1Count { get; set; }
        public int Grade2Count { get; set; }
        public int Grade3Count { get; set; }
        public int Grade4Count { get; set; }
        public int Grade5Count { get; set; }
        public int TotalMeals { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var now = Clock.LocalNow;
        if (Month < 1 || Month > 12) Month = now.Month;
        if (Year < 2020 || Year > 2030) Year = now.Year;

        var start = new DateTime(Year, Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var serviceDays = await Db.LunchServiceDays
            .Where(d => d.Date >= start && d.Date <= end && d.IsAvailable)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var registrations = await Db.LunchRegistrations
            .Where(r => r.Date >= start && r.Date <= end && r.Status == LunchRegistrationStatus.Registered)
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .ToListAsync();

        foreach (var day in serviceDays)
        {
            var dayRegs = registrations.Where(r => r.Date.Date == day.Date.Date).ToList();
            var g1 = dayRegs.Count(r => r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.Grade == 1);
            var g2 = dayRegs.Count(r => r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.Grade == 2);
            var g3 = dayRegs.Count(r => r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.Grade == 3);
            var g4 = dayRegs.Count(r => r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.Grade == 4);
            var g5 = dayRegs.Count(r => r.Student?.ClassAssignments.FirstOrDefault(ca => ca.IsActive)?.Class?.Grade == 5);
            var total = dayRegs.Count;
            var amount = total * day.Price;

            DailyRows.Add(new DailyReportRowDto
            {
                Date = day.Date,
                MenuDescription = day.MenuDescription,
                Grade1Count = g1,
                Grade2Count = g2,
                Grade3Count = g3,
                Grade4Count = g4,
                Grade5Count = g5,
                TotalMeals = total,
                TotalAmount = amount
            });

            TotalMealsServed += total;
            TotalRevenue += amount;
        }

        TotalServingDays = serviceDays.Count;
        if (TotalServingDays > 0)
        {
            AverageMealsPerDay = Math.Round((double)TotalMealsServed / TotalServingDays, 1);
        }

        return Page();
    }
}
