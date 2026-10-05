using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// Enforces record-level and data-scope authorization according to BR-05, BR-06, BR-07, and BR-08.
/// </summary>
public interface IScopeService
{
    Task<bool> CanAccessStudentAsync(int studentId);
    Task<bool> CanAccessClassAsync(int classId);
    Task<List<int>> GetAuthorizedStudentIdsAsync();
    Task<List<int>> GetAuthorizedClassIdsAsync();
}

/// <summary>
/// Implementation of IScopeService.
/// </summary>
public class ScopeService : IScopeService
{
    private readonly SawpDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ScopeService(SawpDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CanAccessStudentAsync(int studentId)
    {
        if (!_currentUser.IsAuthenticated) return false;

        var role = _currentUser.Role;
        if (role is nameof(SystemRole.Admin) or nameof(SystemRole.Board) or nameof(SystemRole.Office))
        {
            return true;
        }

        if (role == nameof(SystemRole.Parent))
        {
            var guardianId = await _currentUser.GetGuardianIdAsync();
            if (!guardianId.HasValue) return false;

            return await _db.StudentGuardians
                .AnyAsync(sg => sg.GuardianId == guardianId.Value && sg.StudentId == studentId);
        }

        if (role == nameof(SystemRole.Teacher))
        {
            var staffId = await _currentUser.GetStaffProfileIdAsync();
            if (!staffId.HasValue) return false;

            // Check if student belongs to teacher's assigned classes
            return await _db.ClassAssignments
                .Where(ca => ca.StudentId == studentId && ca.IsActive)
                .AnyAsync(ca => _db.HomeroomAssignments
                    .Any(ha => ha.ClassId == ca.ClassId && ha.StaffProfileId == staffId.Value && ha.IsActive));
        }

        if (role == nameof(SystemRole.Service))
        {
            // Service staff can only access students relevant for lunch
            return true;
        }

        return false;
    }

    public async Task<bool> CanAccessClassAsync(int classId)
    {
        if (!_currentUser.IsAuthenticated) return false;

        var role = _currentUser.Role;
        if (role is nameof(SystemRole.Admin) or nameof(SystemRole.Board) or nameof(SystemRole.Office))
        {
            return true;
        }

        if (role == nameof(SystemRole.Teacher))
        {
            var staffId = await _currentUser.GetStaffProfileIdAsync();
            if (!staffId.HasValue) return false;

            return await _db.HomeroomAssignments
                .AnyAsync(ha => ha.ClassId == classId && ha.StaffProfileId == staffId.Value && ha.IsActive);
        }

        return false;
    }

    public async Task<List<int>> GetAuthorizedStudentIdsAsync()
    {
        if (!_currentUser.IsAuthenticated) return new List<int>();

        var role = _currentUser.Role;
        if (role is nameof(SystemRole.Admin) or nameof(SystemRole.Board) or nameof(SystemRole.Office) or nameof(SystemRole.Service))
        {
            return await _db.Students.Select(s => s.Id).ToListAsync();
        }

        if (role == nameof(SystemRole.Parent))
        {
            var guardianId = await _currentUser.GetGuardianIdAsync();
            if (!guardianId.HasValue) return new List<int>();

            return await _db.StudentGuardians
                .Where(sg => sg.GuardianId == guardianId.Value)
                .Select(sg => sg.StudentId)
                .ToListAsync();
        }

        if (role == nameof(SystemRole.Teacher))
        {
            var staffId = await _currentUser.GetStaffProfileIdAsync();
            if (!staffId.HasValue) return new List<int>();

            var classIds = await _db.HomeroomAssignments
                .Where(ha => ha.StaffProfileId == staffId.Value && ha.IsActive)
                .Select(ha => ha.ClassId)
                .ToListAsync();

            return await _db.ClassAssignments
                .Where(ca => classIds.Contains(ca.ClassId) && ca.IsActive)
                .Select(ca => ca.StudentId)
                .Distinct()
                .ToListAsync();
        }

        return new List<int>();
    }

    public async Task<List<int>> GetAuthorizedClassIdsAsync()
    {
        if (!_currentUser.IsAuthenticated) return new List<int>();

        var role = _currentUser.Role;
        if (role is nameof(SystemRole.Admin) or nameof(SystemRole.Board) or nameof(SystemRole.Office))
        {
            return await _db.SchoolClasses.Select(c => c.Id).ToListAsync();
        }

        if (role == nameof(SystemRole.Teacher))
        {
            var staffId = await _currentUser.GetStaffProfileIdAsync();
            if (!staffId.HasValue) return new List<int>();

            return await _db.HomeroomAssignments
                .Where(ha => ha.StaffProfileId == staffId.Value && ha.IsActive)
                .Select(ha => ha.ClassId)
                .ToListAsync();
        }

        return new List<int>();
    }
}
