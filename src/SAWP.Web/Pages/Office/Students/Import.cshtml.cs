using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Students;

public class ImportModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public ImportModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty]
    public string CsvData { get; set; } = string.Empty;

    public List<ImportRowPreview> ValidRows { get; set; } = new();
    public List<ImportRowPreview> InvalidRows { get; set; } = new();
    public bool IsPreview { get; set; } = false;

    public class ImportRowPreview
    {
        public int RowIndex { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public Gender Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DateTime AdmissionDate { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public void OnGet()
    {
        // Default sample template
        CsvData = "HS2601020,Phan Đình Tùng,Nam,12/04/2020,15/08/2026,Hà Nội\n" +
                  "HS2601021,Vũ Minh Thư,Nữ,25/09/2020,15/08/2026,Hải Phòng\n" +
                  "HS2601022,Lê Hoàng Long,Nam,03/02/2020,15/08/2026,Hà Nội";
    }

    public async Task<IActionResult> OnPostValidateAsync()
    {
        if (string.IsNullOrWhiteSpace(CsvData))
        {
            Toast("MSG01", "Vui lòng nhập dữ liệu danh sách học sinh cần import.", "error");
            return Page();
        }

        await ParseAndValidateAsync();
        IsPreview = true;

        if (!ValidRows.Any())
        {
            Toast("MSG49");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCommitAsync()
    {
        await ParseAndValidateAsync();

        if (!ValidRows.Any())
        {
            Toast("MSG49");
            IsPreview = true;
            return Page();
        }

        int importedCount = 0;
        foreach (var r in ValidRows)
        {
            var student = new Student
            {
                StudentCode = r.StudentCode,
                FullName = r.FullName,
                Gender = r.Gender,
                DateOfBirth = r.DateOfBirth,
                AdmissionDate = r.AdmissionDate,
                PlaceOfBirth = r.PlaceOfBirth,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            Db.Students.Add(student);
            importedCount++;
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "Import",
            nameof(Student),
            null,
            $"Nhập khẩu hàng loạt {importedCount} học sinh thành công (UC-09)");

        Toast("MSG48", $"Đã nhập thành công {importedCount} học sinh vào hệ thống.", "success");
        return RedirectToPage("/Office/Students/Index");
    }

    private async Task ParseAndValidateAsync()
    {
        ValidRows.Clear();
        InvalidRows.Clear();

        var existingCodes = await Db.Students.Select(s => s.StudentCode).ToListAsync();
        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lines = CsvData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        int lineNum = 0;

        foreach (var line in lines)
        {
            lineNum++;
            var parts = line.Split(new[] { ',', '\t', ';' });
            if (parts.Length < 4)
            {
                InvalidRows.Add(new ImportRowPreview
                {
                    RowIndex = lineNum,
                    ErrorMessage = "Dòng thiếu các cột bắt buộc (cần ít nhất: Mã HS, Họ tên, Giới tính, Ngày sinh)"
                });
                continue;
            }

            var code = parts[0].Trim().ToUpperInvariant();
            var name = parts[1].Trim();
            var genderStr = parts[2].Trim();
            var dobStr = parts[3].Trim();
            var admissionStr = parts.Length > 4 ? parts[4].Trim() : "";
            var pob = parts.Length > 5 ? parts[5].Trim() : "";

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                InvalidRows.Add(new ImportRowPreview { RowIndex = lineNum, StudentCode = code, FullName = name, ErrorMessage = "Mã HS hoặc Họ tên bị trống (MSG01)" });
                continue;
            }

            if (existingCodes.Contains(code))
            {
                InvalidRows.Add(new ImportRowPreview { RowIndex = lineNum, StudentCode = code, FullName = name, ErrorMessage = "Mã học sinh đã tồn tại trong trường (MSG12)" });
                continue;
            }

            if (seenInBatch.Contains(code))
            {
                InvalidRows.Add(new ImportRowPreview { RowIndex = lineNum, StudentCode = code, FullName = name, ErrorMessage = "Mã học sinh bị lặp trong tệp nhập" });
                continue;
            }
            seenInBatch.Add(code);

            var gender = genderStr.ToLower() is "nam" or "male" ? Gender.Male : genderStr.ToLower() is "nữ" or "nu" or "female" ? Gender.Female : Gender.Other;

            if (!DateTime.TryParseExact(dobStr, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            {
                if (!DateTime.TryParse(dobStr, out dob))
                {
                    InvalidRows.Add(new ImportRowPreview { RowIndex = lineNum, StudentCode = code, FullName = name, ErrorMessage = "Định dạng ngày sinh không hợp lệ (cần dd/MM/yyyy)" });
                    continue;
                }
            }

            var admission = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(admissionStr))
            {
                _ = DateTime.TryParse(admissionStr, out admission);
            }

            ValidRows.Add(new ImportRowPreview
            {
                RowIndex = lineNum,
                StudentCode = code,
                FullName = name,
                Gender = gender,
                DateOfBirth = dob,
                AdmissionDate = admission,
                PlaceOfBirth = pob
            });
        }
    }
}
