using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;

namespace SAWP.Web.Services;

/// <summary>
/// Provides identity and contextual profile information for the currently authenticated user.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? Username { get; }
    string? FullName { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    Task<int?> GetStaffProfileIdAsync();
    Task<int?> GetGuardianIdAsync();
    bool IsInRole(string role);
}

/// <summary>
/// Implementation of ICurrentUser reading from HttpContext Claims and DbContext.
/// </summary>
public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SawpDbContext _db;
    private int? _staffProfileId;
    private int? _guardianId;
    private bool _profileLoaded;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, SawpDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int? UserId
    {
        get
        {
            var idClaim = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Username => Principal?.Identity?.Name;

    public string? FullName => Principal?.FindFirst("FullName")?.Value ?? Username;

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public string? Role => Principal?.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public async Task<int?> GetStaffProfileIdAsync()
    {
        if (!_profileLoaded) await LoadProfileAsync();
        return _staffProfileId;
    }

    public async Task<int?> GetGuardianIdAsync()
    {
        if (!_profileLoaded) await LoadProfileAsync();
        return _guardianId;
    }

    private async Task LoadProfileAsync()
    {
        _profileLoaded = true;
        if (!UserId.HasValue) return;

        var staff = await _db.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == UserId.Value && s.IsActive);
        _staffProfileId = staff?.Id;

        var guardian = await _db.Guardians
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == UserId.Value && g.IsActive);
        _guardianId = guardian?.Id;
    }
}
