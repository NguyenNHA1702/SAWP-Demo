using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Admin.Roles;

public class IndexModel : AppPageModel
{
    private readonly IAuditService _auditService;

    public IndexModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public List<RoleDto> Roles { get; set; } = new();
    public List<PermissionGroupDto> PermissionGroups { get; set; } = new();

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int UserCount { get; set; }
        public List<int> AssignedPermissionIds { get; set; } = new();
    }

    public class PermissionGroupDto
    {
        public string Module { get; set; } = string.Empty;
        public List<Permission> Permissions { get; set; } = new();
    }

    [BindProperty(SupportsGet = true)]
    public int? SelectedRoleId { get; set; }

    public RoleDto? SelectedRole { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var rawRoles = await Db.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .OrderBy(r => r.Id)
            .ToListAsync();

        Roles = rawRoles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            DisplayName = r.DisplayName,
            Description = r.Description,
            UserCount = r.UserRoles.Count,
            AssignedPermissionIds = r.RolePermissions.Select(rp => rp.PermissionId).ToList()
        }).ToList();

        if (SelectedRoleId.HasValue && Roles.Any(r => r.Id == SelectedRoleId.Value))
        {
            SelectedRole = Roles.First(r => r.Id == SelectedRoleId.Value);
        }
        else if (Roles.Any())
        {
            SelectedRole = Roles.First();
            SelectedRoleId = SelectedRole.Id;
        }

        var permissions = await Db.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync();
        PermissionGroups = permissions
            .GroupBy(p => string.IsNullOrEmpty(p.Module) ? "Hệ thống" : p.Module)
            .Select(g => new PermissionGroupDto
            {
                Module = g.Key,
                Permissions = g.ToList()
            })
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostUpdatePermissionsAsync(int roleId, List<int> permissionIds)
    {
        // UC-54: Update permissions for role (MSG40)
        var role = await Db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            Toast("MSG09");
            return RedirectToPage();
        }

        // Remove old permissions
        Db.RolePermissions.RemoveRange(role.RolePermissions);

        // Add selected permissions
        if (permissionIds != null)
        {
            foreach (var pid in permissionIds)
            {
                Db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = pid
                });
            }
        }

        await Db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UpdateRolePermissions",
            nameof(Role),
            role.Id.ToString(),
            $"Cập nhật quyền hạn cho vai trò {role.Name}. Tổng quyền: {permissionIds?.Count ?? 0}");

        Toast("MSG40");
        return RedirectToPage(new { SelectedRoleId = roleId });
    }
}
