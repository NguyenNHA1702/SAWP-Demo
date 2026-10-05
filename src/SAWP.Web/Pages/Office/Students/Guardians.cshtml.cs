using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Students;

public class GuardiansModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public GuardiansModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; } // StudentId

    public Student Student { get; set; } = null!;
    public List<StudentGuardian> LinkedGuardians { get; set; } = new();

    // New Guardian Form
    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? IdentityNumber { get; set; }

    [BindProperty]
    public string? Address { get; set; }

    [BindProperty]
    public string? Occupation { get; set; }

    [BindProperty]
    public GuardianRelationship Relationship { get; set; } = GuardianRelationship.Father;

    [BindProperty]
    public bool IsPrimaryContact { get; set; } = true;

    [BindProperty]
    public bool CanPickup { get; set; } = true;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Id = id;
        var student = await Db.Students
            .Include(s => s.StudentGuardians)
            .ThenInclude(sg => sg.Guardian)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Office/Students/Index");
        }

        Student = student;
        LinkedGuardians = student.StudentGuardians.ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostLinkAsync()
    {
        var student = await Db.Students
            .Include(s => s.StudentGuardians)
            .FirstOrDefaultAsync(s => s.Id == Id);

        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Office/Students/Index");
        }

        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(PhoneNumber))
        {
            Toast("MSG01");
            return await OnGetAsync(Id);
        }

        // Find existing guardian by phone or create new
        var guardian = await Db.Guardians.FirstOrDefaultAsync(g => g.PhoneNumber == PhoneNumber.Trim());
        if (guardian == null)
        {
            guardian = new Guardian
            {
                FullName = FullName.Trim(),
                PhoneNumber = PhoneNumber.Trim(),
                Email = Email?.Trim(),
                IdentityNumber = IdentityNumber?.Trim(),
                Address = Address?.Trim(),
                Occupation = Occupation?.Trim(),
                IsActive = true
            };
            Db.Guardians.Add(guardian);
            await Db.SaveChangesAsync();
        }

        // Check if already linked
        var link = await Db.StudentGuardians
            .FirstOrDefaultAsync(sg => sg.StudentId == student.Id && sg.GuardianId == guardian.Id);

        if (link == null)
        {
            // If new link is primary contact, remove primary flag from previous
            if (IsPrimaryContact)
            {
                var existingPrimaries = await Db.StudentGuardians
                    .Where(sg => sg.StudentId == student.Id && sg.IsPrimaryContact)
                    .ToListAsync();
                foreach (var ep in existingPrimaries) ep.IsPrimaryContact = false;
            }

            link = new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = Relationship,
                IsPrimaryContact = IsPrimaryContact,
                CanPickup = CanPickup
            };
            Db.StudentGuardians.Add(link);
            await Db.SaveChangesAsync();

            await _auditService.LogAsync(
                "LinkGuardian",
                nameof(StudentGuardian),
                link.Id.ToString(),
                $"Liên kết phụ huynh {guardian.FullName} ({Relationship}) với học sinh {student.FullName}");

            Toast("MSG13");
        }
        else
        {
            Toast("MSG01", "Phụ huynh này đã được liên kết với học sinh.", "info");
        }

        return RedirectToPage("/Office/Students/Guardians", new { id = Id });
    }

    public async Task<IActionResult> OnPostUnlinkAsync(int linkId)
    {
        var link = await Db.StudentGuardians
            .Include(sg => sg.Student)
            .Include(sg => sg.Guardian)
            .FirstOrDefaultAsync(sg => sg.Id == linkId && sg.StudentId == Id);

        if (link == null)
        {
            return RedirectToPage("/Office/Students/Guardians", new { id = Id });
        }

        // UC-10 E1: Cannot unlink if it leaves active student without required primary guardian (BR-13)
        var otherGuardians = await Db.StudentGuardians
            .Where(sg => sg.StudentId == Id && sg.Id != linkId)
            .ToListAsync();

        if (!otherGuardians.Any())
        {
            Toast("MSG01", "Không thể hủy liên kết phụ huynh duy nhất của học sinh (BR-13).", "error");
            return await OnGetAsync(Id);
        }

        if (link.IsPrimaryContact && !otherGuardians.Any(og => og.IsPrimaryContact))
        {
            // Set first remaining guardian as primary
            otherGuardians.First().IsPrimaryContact = true;
        }

        Db.StudentGuardians.Remove(link);
        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UnlinkGuardian",
            nameof(StudentGuardian),
            linkId.ToString(),
            $"Hủy liên kết phụ huynh {link.Guardian.FullName} với học sinh {link.Student.FullName}");

        Toast("MSG13", "Đã hủy liên kết phụ huynh thành công.", "success");
        return RedirectToPage("/Office/Students/Guardians", new { id = Id });
    }
}
