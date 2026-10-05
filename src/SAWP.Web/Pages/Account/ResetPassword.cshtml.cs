using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class ResetPasswordModel : AppPageModel
{
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;

    public ResetPasswordModel(IPasswordService passwordService, IAuditService auditService)
    {
        _passwordService = passwordService;
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

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
        if (NewPassword != ConfirmPassword)
        {
            Toast("MSG04");
            return Page();
        }

        if (!_passwordService.ValidateComplexity(NewPassword, out _))
        {
            Toast("MSG04");
            return Page();
        }

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null || user.OtpCode != Token)
        {
            Toast("MSG01", "Yêu cầu đặt lại mật khẩu không hợp lệ.", "error");
            return RedirectToPage("/Account/ForgotPassword");
        }

        user.PasswordHash = _passwordService.HashPassword(user, NewPassword);
        user.OtpCode = null;
        user.OtpExpiry = null;
        await Db.SaveChangesAsync();

        await _auditService.LogAsync("PasswordReset", nameof(User), user.Id.ToString(), $"Đặt lại mật khẩu thành công qua OTP cho email {Email}");

        Toast("MSG46");
        return RedirectToPage("/Account/Login");
    }
}
