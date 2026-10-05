using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Admin.Users;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;
    private readonly IPasswordService _passwordService;

    public IndexModel(IAuditService auditService, IPasswordService passwordService)
    {
        _auditService = auditService;
        _passwordService = passwordService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public UserStatus? StatusFilter { get; set; }

    public List<UserViewModel> Users { get; set; } = new();
    public List<Role> AvailableRoles { get; set; } = new();
    public List<StaffProfile> UnlinkedStaff { get; set; } = new();
    public List<Guardian> UnlinkedGuardians { get; set; } = new();

    public class UserViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public string LinkedProfileInfo { get; set; } = "Chưa liên kết";
        public bool IsStaffLinked { get; set; }
        public bool IsGuardianLinked { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        AvailableRoles = await Db.Roles.OrderBy(r => r.Name).ToListAsync();

        var query = Db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
            .Include(u => u.Guardian)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(s) ||
                                     u.FullName.ToLower().Contains(s) ||
                                     (u.Email != null && u.Email.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(RoleFilter))
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == RoleFilter));
        }

        if (StatusFilter.HasValue)
        {
            query = query.Where(u => u.Status == StatusFilter.Value);
        }

        var rawUsers = await query
            .OrderBy(u => u.Username)
            .ToListAsync();

        Users = rawUsers.Select(u =>
        {
            string linkedInfo = "Chưa liên kết";
            var staff = u.StaffProfile;
            var guardian = u.Guardian;

            if (staff != null) linkedInfo = $"CB: {staff.FullName} ({staff.Department})";
            else if (guardian != null) linkedInfo = $"PH: {guardian.FullName} ({guardian.PhoneNumber})";

            return new UserViewModel
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                LastLoginAt = u.LastLoginAt,
                Roles = u.UserRoles.Select(ur => ur.Role.DisplayName ?? ur.Role.Name).ToList(),
                LinkedProfileInfo = linkedInfo,
                IsStaffLinked = staff != null,
                IsGuardianLinked = guardian != null
            };
        }).ToList();

        // Load unlinked profiles for linking modal
        UnlinkedStaff = await Db.StaffProfiles
            .Where(s => s.UserId == null && s.IsActive)
            .OrderBy(s => s.FullName)
            .Take(50)
            .ToListAsync();

        UnlinkedGuardians = await Db.Guardians
            .Where(g => g.UserId == null && g.IsActive)
            .OrderBy(g => g.FullName)
            .Take(50)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostCreateUserAsync(string username, string fullName, string email, string? phoneNumber, string password, int roleId)
    {
        // UC-53: Create user (BR-01, BR-02, BR-04, MSG38)
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
        {
            Toast("MSG01", "Vui lòng nhập đầy đủ tên đăng nhập, họ tên và mật khẩu.", "error");
            return RedirectToPage();
        }

        if (await Db.Users.AnyAsync(u => u.Username.ToLower() == username.Trim().ToLower()))
        {
            Toast("MSG01", "Tên đăng nhập đã tồn tại trong hệ thống.", "error");
            return RedirectToPage();
        }

        if (!_passwordService.ValidateComplexity(password, out var err))
        {
            Toast("MSG04", err ?? Messages.Get("MSG04"), "error");
            return RedirectToPage();
        }

        var user = new User
        {
            Username = username.Trim().ToLower(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLower(),
            PhoneNumber = phoneNumber?.Trim(),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordService.HashPassword(user, password);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var role = await Db.Roles.FindAsync(roleId);
        if (role != null)
        {
            Db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await Db.SaveChangesAsync();
        }

        await _auditService.LogAsync(
            "CreateUser",
            nameof(User),
            user.Id.ToString(),
            $"Tạo tài khoản {user.Username} ({user.FullName}) với vai trò {role?.Name ?? "None"}");

        Toast("MSG38");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        // BR-52: Soft deactivation of user account (MSG39)
        var user = await Db.Users.FindAsync(id);
        if (user == null)
        {
            Toast("MSG09");
            return RedirectToPage();
        }

        if (user.Id == CurrentUser.UserId)
        {
            Toast("MSG09", "Không thể tự vô hiệu hóa tài khoản quản trị đang đăng nhập.", "error");
            return RedirectToPage();
        }

        user.Status = user.Status == UserStatus.Active ? UserStatus.Inactive : UserStatus.Active;
        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "ToggleUserStatus",
            nameof(User),
            user.Id.ToString(),
            $"Chuyển trạng thái tài khoản {user.Username} sang {user.Status}");

        if (user.Status == UserStatus.Inactive)
        {
            Toast("MSG39");
        }
        else
        {
            Toast("MSG08", "Đã kích hoạt lại tài khoản.", "success");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int id, string? newPassword)
    {
        // UC-60: Admin reset user password
        var user = await Db.Users.FindAsync(id);
        if (user == null)
        {
            Toast("MSG09");
            return RedirectToPage();
        }

        var pwd = string.IsNullOrWhiteSpace(newPassword) ? "Demo@123" : newPassword.Trim();
        if (!_passwordService.ValidateComplexity(pwd, out var err))
        {
            Toast("MSG04", err ?? Messages.Get("MSG04"), "error");
            return RedirectToPage();
        }

        user.PasswordHash = _passwordService.HashPassword(user, pwd);
        user.Status = UserStatus.Active; // unlock if locked
        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "AdminResetPassword",
            nameof(User),
            user.Id.ToString(),
            $"Quản trị viên đặt lại mật khẩu cho tài khoản {user.Username}");

        Toast("MSG46", $"Đã đặt lại mật khẩu cho {user.Username} thành công.", "success");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLinkProfileAsync(int userId, string profileType, int profileId)
    {
        // UC-55: Link user to Staff or Guardian profile
        var user = await Db.Users.FindAsync(userId);
        if (user == null)
        {
            Toast("MSG09");
            return RedirectToPage();
        }

        if (profileType == "Staff")
        {
            var staff = await Db.StaffProfiles.FindAsync(profileId);
            if (staff != null)
            {
                staff.UserId = user.Id;
                await Db.SaveChangesAsync();
                await _auditService.LogAsync(
                    "LinkStaffProfile",
                    nameof(StaffProfile),
                    staff.Id.ToString(),
                    $"Liên kết tài khoản {user.Username} với cán bộ {staff.FullName}");
                Toast("MSG08", $"Đã liên kết tài khoản {user.Username} với cán bộ {staff.FullName}.", "success");
            }
        }
        else if (profileType == "Guardian")
        {
            var guardian = await Db.Guardians.FindAsync(profileId);
            if (guardian != null)
            {
                guardian.UserId = user.Id;
                await Db.SaveChangesAsync();
                await _auditService.LogAsync(
                    "LinkGuardianProfile",
                    nameof(Guardian),
                    guardian.Id.ToString(),
                    $"Liên kết tài khoản {user.Username} với phụ huynh {guardian.FullName}");
                Toast("MSG13");
            }
        }

        return RedirectToPage();
    }
}
