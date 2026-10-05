using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Office.Students;

public class IndexModel : AppPageModel
{
    public List<StudentItemViewModel> Students { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Grade { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ClassId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public List<SchoolClass> AvailableClasses { get; set; } = new();

    public class StudentItemViewModel
    {
        public int Id { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public Gender Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? ClassName { get; set; }
        public int? Grade { get; set; }
        public string PrimaryGuardianName { get; set; } = "Chưa liên kết";
        public string PrimaryGuardianPhone { get; set; } = "-";
        public string Status { get; set; } = "Active";
    }

    public async Task OnGetAsync()
    {
        AvailableClasses = await Db.SchoolClasses
            .Where(c => c.IsActive)
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var query = Db.Students
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
            .ThenInclude(ca => ca.Class)
            .Include(s => s.StudentGuardians)
            .ThenInclude(sg => sg.Guardian)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(st => st.FullName.ToLower().Contains(s) || st.StudentCode.ToLower().Contains(s));
        }

        if (Grade.HasValue && Grade.Value > 0)
        {
            query = query.Where(st => st.ClassAssignments.Any(ca => ca.IsActive && ca.Class.Grade == Grade.Value));
        }

        if (ClassId.HasValue && ClassId.Value > 0)
        {
            query = query.Where(st => st.ClassAssignments.Any(ca => ca.IsActive && ca.ClassId == ClassId.Value));
        }

        if (!string.IsNullOrWhiteSpace(Status))
        {
            query = query.Where(st => st.Status == Status);
        }

        var list = await query
            .OrderBy(st => st.StudentCode)
            .Take(100)
            .ToListAsync();

        Students = list.Select(st =>
        {
            var activeClass = st.ClassAssignments.FirstOrDefault()?.Class;
            var primaryGuardian = st.StudentGuardians.FirstOrDefault(g => g.IsPrimaryContact)?.Guardian
                               ?? st.StudentGuardians.FirstOrDefault()?.Guardian;

            return new StudentItemViewModel
            {
                Id = st.Id,
                StudentCode = st.StudentCode,
                FullName = st.FullName,
                Gender = st.Gender,
                DateOfBirth = st.DateOfBirth,
                ClassName = activeClass?.Name,
                Grade = activeClass?.Grade,
                PrimaryGuardianName = primaryGuardian?.FullName ?? "Chưa liên kết",
                PrimaryGuardianPhone = primaryGuardian?.PhoneNumber ?? "-",
                Status = st.Status
            };
        }).ToList();
    }
}
