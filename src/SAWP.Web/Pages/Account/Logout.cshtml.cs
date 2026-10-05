using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class LogoutModel : AppPageModel
{
    private readonly IAuditService _auditService;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(IAuditService auditService, ILogger<LogoutModel> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        return await PerformLogoutAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        return await PerformLogoutAsync();
    }

    private async Task<IActionResult> PerformLogoutAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var username = User.Identity.Name;
            await _auditService.LogAsync("Logout", nameof(User), CurrentUser.UserId?.ToString(), "Đăng xuất khỏi hệ thống");
            _logger.LogInformation("[UC-05] User {Username} logged out.", username);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Toast("MSG06");
        return RedirectToPage("/Account/Login");
    }
}
