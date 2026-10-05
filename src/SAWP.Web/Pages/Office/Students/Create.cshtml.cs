using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Students;

public class CreateModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public CreateModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string StudentCode { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public DateTime DateOfBirth { get; set; } = new DateTime(2020, 1, 1);

    [BindProperty]
    public Gender Gender { get; set; } = Gender.Male;

    [BindProperty]
    public DateTime AdmissionDate { get; set; } = DateTime.Today;

    [BindProperty]
    public string? PlaceOfBirth { get; set; }

    [BindProperty]
    public string? Address { get; set; }

    [BindProperty]
    public string? HealthNotes { get; set; }

    [BindProperty]
    public int? InitialClassId { get; set; }

    public List<SchoolClass> AvailableClasses { get; set; } = new();

    public async Task OnGetAsync()
    {
        AvailableClasses = await Db.SchoolClasses
            .Where(c => c.IsActive)
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(StudentCode) || string.IsNullOrWhiteSpace(FullName))
        {
            Toast("MSG01");
            await OnGetAsync();
            return Page();
        }

        // UC-07 E2: Check uniqueness of StudentCode (BR-09)
        var exists = await Db.Students.AnyAsync(s => s.StudentCode == StudentCode.Trim());
        if (exists)
        {
            Toast("MSG12");
            await OnGetAsync();
            return Page();
        }

        var student = new Student
        {
            StudentCode = StudentCode.Trim().ToUpperInvariant(),
            FullName = FullName.Trim(),
            DateOfBirth = DateOfBirth,
            Gender = Gender,
            AdmissionDate = AdmissionDate,
            PlaceOfBirth = PlaceOfBirth?.Trim(),
            Address = Address?.Trim(),
            HealthNotes = HealthNotes?.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        Db.Students.Add(student);
        await Db.SaveChangesAsync();

        // Optional initial class assignment
        if (InitialClassId.HasValue && InitialClassId.Value > 0)
        {
            var assignment = new ClassAssignment
            {
                StudentId = student.Id,
                ClassId = InitialClassId.Value,
                StartDate = DateTime.Today,
                IsActive = true
            };
            Db.ClassAssignments.Add(assignment);
            await Db.SaveChangesAsync();
        }

        // Audit log (BR-11)
        await _auditService.LogAsync(
            "Create",
            nameof(Student),
            student.Id.ToString(),
            $"Tạo hồ sơ học sinh {student.FullName} (Mã: {student.StudentCode})");

        Toast("MSG08");
        return RedirectToPage("/Office/Students/Detail", new { id = student.Id });
    }
}
