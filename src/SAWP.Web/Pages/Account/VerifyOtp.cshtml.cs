using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class VerifyOtpModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public VerifyOtpModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string OtpCode { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(OtpCode))
        {
            Toast("MSG01");
            return Page();
        }

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null || user.OtpCode != OtpCode.Trim() || user.OtpExpiry < DateTime.UtcNow)
        {
            Toast("MSG01", "Mã xác thực OTP không chính xác hoặc đã hết hạn.", "error");
            return Page();
        }

        await _auditService.LogAsync("OtpVerified", nameof(User), user.Id.ToString(), $"Xác thực OTP thành công cho email {Email}");

        return RedirectToPage("/Account/ResetPassword", new { email = Email, token = user.OtpCode });
    }
}
