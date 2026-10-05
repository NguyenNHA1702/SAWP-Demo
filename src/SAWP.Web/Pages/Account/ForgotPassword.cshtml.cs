using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class ForgotPasswordModel : AppPageModel
{
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        IEmailService emailService,
        IAuditService auditService,
        ILogger<ForgotPasswordModel> logger)
    {
        _emailService = emailService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Toast("MSG01");
            return Page();
        }

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user != null)
        {
            // Generate 6-digit OTP
            var otp = new Random().Next(100000, 999999).ToString();
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(15);
            await Db.SaveChangesAsync();

            // Send simulated email
            var body = $"Xin chào {user.FullName},\n\nMã xác thực OTP để đặt lại mật khẩu cho tài khoản SAWP của bạn là: {otp}\nMã này có hiệu lực trong 15 phút.\nNếu bạn không yêu cầu, vui lòng bỏ qua email này.";
            await _emailService.SendEmailAsync(user.Email, "Mã xác thực đặt lại mật khẩu SAWP", body, otp);

            await _auditService.LogAsync("OtpRequested", nameof(User), user.Id.ToString(), $"Yêu cầu OTP đặt lại mật khẩu cho email {Email}");
            _logger.LogInformation("[Auth] OTP generated for email {Email}", Email);
        }

        // Even if user not found, don't disclose existence, redirect to VerifyOtp with email query
        return RedirectToPage("/Account/VerifyOtp", new { email = Email });
    }
}
