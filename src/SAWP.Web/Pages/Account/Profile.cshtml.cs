using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class ProfileModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public ProfileModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string? PhoneNumber { get; set; }

    public string Username { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public StaffProfile? Staff { get; set; }
    public Guardian? GuardianProfile { get; set; }
    public List<Student> LinkedChildren { get; set; } = new();
    public SchoolClass? AssignedClass { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CurrentUser.UserId.HasValue) return RedirectToPage("/Account/Login");

        var user = await Db.Users
            .Include(u => u.StaffProfile)
            .Include(u => u.Guardian)
            .FirstOrDefaultAsync(u => u.Id == CurrentUser.UserId.Value);

        if (user == null) return RedirectToPage("/Account/Login");

        Username = user.Username;
        FullName = user.FullName;
        Email = user.Email;
        PhoneNumber = user.PhoneNumber;
        RoleName = CurrentUser.Role ?? "";
        Staff = user.StaffProfile;
        GuardianProfile = user.Guardian;

        // UC-02 A1: If parent, load linked children
        if (GuardianProfile != null)
        {
            LinkedChildren = await Db.StudentGuardians
                .Where(sg => sg.GuardianId == GuardianProfile.Id)
                .Include(sg => sg.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
                .Select(sg => sg.Student)
                .ToListAsync();
        }

        // If teacher, load assigned homeroom class
        if (Staff != null)
        {
            var hr = await Db.HomeroomAssignments
                .Include(h => h.Class)
                .FirstOrDefaultAsync(h => h.StaffProfileId == Staff.Id && h.IsActive);
            AssignedClass = hr?.Class;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CurrentUser.UserId.HasValue) return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email))
        {
            Toast("MSG01");
            return await OnGetAsync();
        }

        var user = await Db.Users
            .Include(u => u.StaffProfile)
            .Include(u => u.Guardian)
            .FirstOrDefaultAsync(u => u.Id == CurrentUser.UserId.Value);

        if (user == null) return RedirectToPage("/Account/Login");

        var oldInfo = $"FullName={user.FullName}, Email={user.Email}, Phone={user.PhoneNumber}";

        user.FullName = FullName.Trim();
        user.Email = Email.Trim();
        user.PhoneNumber = PhoneNumber?.Trim();

        // UC-03 A1: Synchronize with linked guardian / staff profile
        if (user.Guardian != null)
        {
            user.Guardian.FullName = user.FullName;
            user.Guardian.Email = user.Email;
            user.Guardian.PhoneNumber = user.PhoneNumber ?? "";
        }

        if (user.StaffProfile != null)
        {
            user.StaffProfile.FullName = user.FullName;
            user.StaffProfile.Email = user.Email;
            user.StaffProfile.PhoneNumber = user.PhoneNumber;
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UpdateProfile",
            nameof(User),
            user.Id.ToString(),
            $"Cập nhật hồ sơ cá nhân. Cũ: [{oldInfo}] -> Mới: [FullName={user.FullName}, Email={user.Email}, Phone={user.PhoneNumber}]");

        Toast("MSG47");
        return RedirectToPage("/Account/Profile");
    }
}
