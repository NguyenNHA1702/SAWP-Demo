using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Service.Lunch;

public class MenuModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public MenuModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; }

    public List<LunchServiceDay> ServiceDays { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var now = Clock.LocalNow;
        if (Month < 1 || Month > 12) Month = now.Month;
        if (Year < 2020 || Year > 2030) Year = now.Year;

        var start = new DateTime(Year, Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        ServiceDays = await Db.LunchServiceDays
            .Where(d => d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostSaveDayAsync(int? id, DateTime date, bool isAvailable, string cutoffTime, decimal price, string? menuDescription, string? notes)
    {
        if (price <= 0) price = 35000;
        if (!TimeSpan.TryParse(cutoffTime, out var cutoff))
        {
            cutoff = new TimeSpan(8, 0, 0);
        }

        LunchServiceDay? day;
        if (id.HasValue && id.Value > 0)
        {
            day = await Db.LunchServiceDays.FindAsync(id.Value);
            if (day == null)
            {
                Toast("MSG09");
                return RedirectToPage(new { Month, Year });
            }

            day.IsAvailable = isAvailable;
            day.CutoffTime = cutoff;
            day.Price = price;
            day.MenuDescription = menuDescription?.Trim();
            day.Notes = notes?.Trim();
        }
        else
        {
            day = await Db.LunchServiceDays.FirstOrDefaultAsync(d => d.Date == date.Date);
            if (day == null)
            {
                day = new LunchServiceDay
                {
                    Date = date.Date,
                    IsAvailable = isAvailable,
                    CutoffTime = cutoff,
                    Price = price,
                    MenuDescription = menuDescription?.Trim(),
                    Notes = notes?.Trim()
                };
                Db.LunchServiceDays.Add(day);
            }
            else
            {
                day.IsAvailable = isAvailable;
                day.CutoffTime = cutoff;
                day.Price = price;
                day.MenuDescription = menuDescription?.Trim();
                day.Notes = notes?.Trim();
            }
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "SaveLunchMenu",
            nameof(LunchServiceDay),
            day.Id.ToString(),
            $"Cập nhật thực đơn ngày {FormatDate(day.Date)}: {day.MenuDescription} (Giá: {day.Price:N0}đ)");

        Toast("MSG08");
        return RedirectToPage(new { Month = day.Date.Month, Year = day.Date.Year });
    }

    public async Task<IActionResult> OnPostAutoGenerateMonthAsync(int month, int year)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var existingDates = await Db.LunchServiceDays
            .Where(d => d.Date >= start && d.Date <= end)
            .Select(d => d.Date.Date)
            .ToListAsync();

        string[] defaultMenus = new[]
        {
            "Cơm trắng, Thịt lợn kho trứng cút, Canh rau cải thìa thịt băm, Tráng miệng chuối tiêu",
            "Cơm trắng, Gà om nấm đông cô, Canh bí đỏ nấu tôm, Tráng miệng thanh long",
            "Cơm trắng, Cá phi lê chiên xù, Canh mồng tơi cua đồng, Tráng miệng ổi hồng",
            "Cơm trắng, Bò xào rau củ ngũ sắc, Canh sườn nấu chua bắp cải, Tráng miệng táo",
            "Cơm trắng, Sườn lợn nướng mật ong, Canh rau muống luộc dầm sấu, Tráng miệng sữa chua"
        };

        int addedCount = 0;
        int menuIdx = 0;

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !existingDates.Contains(d.Date))
            {
                var day = new LunchServiceDay
                {
                    Date = d.Date,
                    IsAvailable = true,
                    CutoffTime = new TimeSpan(8, 0, 0),
                    Price = 35000,
                    MenuDescription = defaultMenus[menuIdx % defaultMenus.Length]
                };
                Db.LunchServiceDays.Add(day);
                addedCount++;
                menuIdx++;
            }
        }

        if (addedCount > 0)
        {
            await Db.SaveChangesAsync();
            await _auditService.LogAsync(
                "GenerateLunchMonth",
                nameof(LunchServiceDay),
                $"{year}-{month:D2}",
                $"Tự động tạo {addedCount} ngày ăn bán trú cho tháng {month}/{year}");
        }

        Toast("MSG08", $"Đã tạo thành công {addedCount} ngày ăn bán trú cho tháng {month}/{year}.", "success");
        return RedirectToPage(new { Month = month, Year = year });
    }
}
