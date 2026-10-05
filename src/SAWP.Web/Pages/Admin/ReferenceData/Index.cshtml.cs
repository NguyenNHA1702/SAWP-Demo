using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Admin.ReferenceData;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public IndexModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedCategory { get; set; }

    public List<string> Categories { get; set; } = new();
    public List<SAWP.Web.Domain.ReferenceData> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // Seed default reference data if table is empty
        if (!await Db.ReferenceDataList.AnyAsync())
        {
            Db.ReferenceDataList.AddRange(
                new SAWP.Web.Domain.ReferenceData { Category = "DocumentType", Code = "GXN_HOC", Name = "Giấy xác nhận đang học tại trường", SortOrder = 1 },
                new SAWP.Web.Domain.ReferenceData { Category = "DocumentType", Code = "GXN_CC", Name = "Giấy xác nhận chuyên cần và rèn luyện", SortOrder = 2 },
                new SAWP.Web.Domain.ReferenceData { Category = "DocumentType", Code = "GXN_HB", Name = "Bản sao học bạ / Hồ sơ chuyển trường", SortOrder = 3 },
                new SAWP.Web.Domain.ReferenceData { Category = "Relationship", Code = "BO", Name = "Bố", SortOrder = 1 },
                new SAWP.Web.Domain.ReferenceData { Category = "Relationship", Code = "ME", Name = "Mẹ", SortOrder = 2 },
                new SAWP.Web.Domain.ReferenceData { Category = "Relationship", Code = "GIAM_HO", Name = "Người giám hộ hợp pháp", SortOrder = 3 },
                new SAWP.Web.Domain.ReferenceData { Category = "CancelReason", Code = "BENH", Name = "Học sinh bị bệnh đột xuất", SortOrder = 1 },
                new SAWP.Web.Domain.ReferenceData { Category = "CancelReason", Code = "VIEC_GD", Name = "Gia đình có việc bận đột xuất", SortOrder = 2 }
            );
            await Db.SaveChangesAsync();
        }

        Categories = await Db.ReferenceDataList
            .Select(r => r.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        if (string.IsNullOrEmpty(SelectedCategory) && Categories.Any())
        {
            SelectedCategory = Categories.First();
        }

        Items = await Db.ReferenceDataList
            .Where(r => r.Category == SelectedCategory)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostSaveItemAsync(int? id, string category, string code, string name, int sortOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            Toast("MSG01", "Vui lòng nhập đầy đủ mã và tên danh mục.", "error");
            return RedirectToPage(new { SelectedCategory = category });
        }

        SAWP.Web.Domain.ReferenceData? item;
        if (id.HasValue && id.Value > 0)
        {
            item = await Db.ReferenceDataList.FindAsync(id.Value);
            if (item != null)
            {
                item.Code = code.Trim();
                item.Name = name.Trim();
                item.SortOrder = sortOrder;
                item.IsActive = isActive;
            }
        }
        else
        {
            item = new SAWP.Web.Domain.ReferenceData
            {
                Category = category.Trim(),
                Code = code.Trim(),
                Name = name.Trim(),
                SortOrder = sortOrder,
                IsActive = isActive
            };
            Db.ReferenceDataList.Add(item);
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "SaveReferenceData",
            nameof(SAWP.Web.Domain.ReferenceData),
            item?.Id.ToString() ?? "0",
            $"Cập nhật danh mục {category}: [{code}] {name}");

        Toast("MSG08");
        return RedirectToPage(new { SelectedCategory = category });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var item = await Db.ReferenceDataList.FindAsync(id);
        if (item != null)
        {
            item.IsActive = !item.IsActive;
            await Db.SaveChangesAsync();
            Toast("MSG08");
        }
        return RedirectToPage(new { SelectedCategory = item?.Category });
    }
}
