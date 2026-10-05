using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Teacher.Class;

public class RosterModel : AppPageModel
{
    public SchoolClass? AssignedClass { get; set; }
    public List<StudentRosterItem> Students { get; set; } = new();

    public class StudentRosterItem
    {
        public int Id { get; set; }
        public int? RollNumber { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public Gender Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string PrimaryGuardianName { get; set; } = "Chưa liên kết";
        public string PrimaryGuardianPhone { get; set; } = "-";
        public string HealthNotes { get; set; } = "Bình thường";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return RedirectToPage("/Teacher/Index");

        var hr = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (hr == null)
        {
            Toast("MSG20");
            return RedirectToPage("/Teacher/Index");
        }

        AssignedClass = hr.Class;

        var assignments = await Db.ClassAssignments
            .Where(ca => ca.ClassId == AssignedClass.Id && ca.IsActive)
            .Include(ca => ca.Student)
            .ThenInclude(s => s.StudentGuardians)
            .ThenInclude(sg => sg.Guardian)
            .OrderBy(ca => ca.RollNumber ?? 999)
            .ThenBy(ca => ca.Student.FullName)
            .ToListAsync();

        Students = assignments.Select(ca =>
        {
            var s = ca.Student;
            var guardian = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Guardian
                        ?? s.StudentGuardians.FirstOrDefault()?.Guardian;

            return new StudentRosterItem
            {
                Id = s.Id,
                RollNumber = ca.RollNumber,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                Gender = s.Gender,
                DateOfBirth = s.DateOfBirth,
                PrimaryGuardianName = guardian?.FullName ?? "Chưa liên kết",
                PrimaryGuardianPhone = guardian?.PhoneNumber ?? "-",
                HealthNotes = s.HealthNotes ?? "Bình thường"
            };
        }).ToList();

        return Page();
    }
}
