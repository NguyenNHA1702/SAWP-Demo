using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Students;

public class EditModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public EditModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string StudentCode { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public DateTime DateOfBirth { get; set; }

    [BindProperty]
    public Gender Gender { get; set; }

    [BindProperty]
    public string? PlaceOfBirth { get; set; }

    [BindProperty]
    public string? Address { get; set; }

    [BindProperty]
    public string? HealthNotes { get; set; }

    [BindProperty]
    public string Status { get; set; } = "Active";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var student = await Db.Students.FindAsync(id);
        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Office/Students/Index");
        }

        Id = student.Id;
        StudentCode = student.StudentCode;
        FullName = student.FullName;
        DateOfBirth = student.DateOfBirth;
        Gender = student.Gender;
        PlaceOfBirth = student.PlaceOfBirth;
        Address = student.Address;
        HealthNotes = student.HealthNotes;
        Status = student.Status;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            Toast("MSG01");
            return Page();
        }

        var student = await Db.Students.FindAsync(Id);
        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Office/Students/Index");
        }

        var oldData = $"FullName={student.FullName}, Status={student.Status}, HealthNotes={student.HealthNotes}";

        student.FullName = FullName.Trim();
        student.DateOfBirth = DateOfBirth;
        student.Gender = Gender;
        student.PlaceOfBirth = PlaceOfBirth?.Trim();
        student.Address = Address?.Trim();
        student.HealthNotes = HealthNotes?.Trim();
        student.Status = Status;

        await Db.SaveChangesAsync();

        // Audit log (BR-11)
        await _auditService.LogAsync(
            "Update",
            nameof(Student),
            student.Id.ToString(),
            $"Cập nhật hồ sơ học sinh {student.FullName}. Cũ: [{oldData}]");

        Toast("MSG08");
        return RedirectToPage("/Office/Students/Detail", new { id = student.Id });
    }
}
