using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class ChangePasswordModel : AppPageModel
{
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;

    public ChangePasswordModel(IPasswordService passwordService, IAuditService auditService)
    {
        _passwordService = passwordService;
        _auditService = auditService;
    }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    [Compare("NewPassword", ErrorMessage = "MSG04")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CurrentUser.UserId.HasValue) return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            Toast("MSG01");
            return Page();
        }

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUser.UserId.Value);
        if (user == null) return RedirectToPage("/Account/Login");

        // UC-04 E1: Current password incorrect
        if (!_passwordService.VerifyPassword(user, user.PasswordHash, CurrentPassword))
        {
            Toast("MSG02");
            return Page();
        }

        // UC-04 E2: Password policy validation
        if (NewPassword != ConfirmPassword || !_passwordService.ValidateComplexity(NewPassword, out _))
        {
            Toast("MSG04");
            return Page();
        }

        user.PasswordHash = _passwordService.HashPassword(user, NewPassword);
        user.MustChangePassword = false;
        await Db.SaveChangesAsync();

        await _auditService.LogAsync("ChangePassword", nameof(User), user.Id.ToString(), "Người dùng tự đổi mật khẩu thành công (UC-04)");

        Toast("MSG46");
        return RedirectToPage("/Account/Profile");
    }
}
