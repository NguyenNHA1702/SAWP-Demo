using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Account;

public class LoginModel : AppPageModel
{
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IPasswordService passwordService,
        IAuditService auditService,
        ILogger<LoginModel> logger)
    {
        _passwordService = passwordService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "MSG01")]
    public string Password { get; set; } = string.Empty;

    public IActionResult OnGet(string? returnUrl = null)
    {
        // UC-01 Alternative Flow A1: If already authenticated, redirect to role dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Toast("MSG01");
            return Page();
        }

        // 1. Find user by username or email
        var user = await Db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == Username || u.Email == Username);

        // UC-01 E1: Invalid Credentials
        if (user == null || !_passwordService.VerifyPassword(user, user.PasswordHash, Password))
        {
            _logger.LogWarning("[UC-01] Failed login attempt for identifier {Identifier}", Username);
            Toast("MSG02");
            return Page();
        }

        // UC-01 E2: Inactive or Locked Account
        if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning("[UC-01] Denied login for inactive/locked account {Username} (Status={Status})", user.Username, user.Status);
            Toast("MSG03");
            return Page();
        }

        // 2. Identify Role
        var role = user.UserRoles.FirstOrDefault()?.Role.Name ?? nameof(SystemRole.Parent);

        // 3. Create Claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("FullName", user.FullName),
            new(ClaimTypes.Role, role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTime.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Update LastLogin
        user.LastLoginAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        await _auditService.LogAsync("Login", nameof(User), user.Id.ToString(), $"Đăng nhập thành công với vai trò {role}");
        _logger.LogInformation("[UC-01] User {Username} logged in successfully with Role {Role}", user.Username, role);

        Toast("MSG05");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToRoleDashboard(role);
    }

    private IActionResult RedirectToRoleDashboard(string? role = null)
    {
        role ??= CurrentUser.Role;

        return role switch
        {
            nameof(SystemRole.Parent) => RedirectToPage("/Parent/Index"),
            nameof(SystemRole.Teacher) => RedirectToPage("/Teacher/Index"),
            nameof(SystemRole.Office) => RedirectToPage("/Office/Index"),
            nameof(SystemRole.Service) => RedirectToPage("/Service/Index"),
            nameof(SystemRole.Board) => RedirectToPage("/Board/Index"),
            nameof(SystemRole.Admin) => RedirectToPage("/Admin/Index"),
            _ => RedirectToPage("/Index")
        };
    }
}
