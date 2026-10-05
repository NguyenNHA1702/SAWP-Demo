using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;

namespace SAWP.Web.Pages.Teacher;

public class IndexModel : AppPageModel
{
    public SchoolClass? AssignedClass { get; set; }
    public int TotalStudents { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int LunchCount { get; set; }
    public List<AdministrativeRequest> PendingLeaveRequests { get; set; } = new();

    public async Task OnGetAsync()
    {
        var staffId = await CurrentUser.GetStaffProfileIdAsync();
        if (!staffId.HasValue) return;

        var hr = await Db.HomeroomAssignments
            .Include(h => h.Class)
            .FirstOrDefaultAsync(h => h.StaffProfileId == staffId.Value && h.IsActive);

        if (hr != null)
        {
            AssignedClass = hr.Class;

            var studentIds = await Db.ClassAssignments
                .Where(ca => ca.ClassId == AssignedClass.Id && ca.IsActive)
                .Select(ca => ca.StudentId)
                .ToListAsync();

            TotalStudents = studentIds.Count;

            var today = Clock.Today;

            // Attendance today
            var attendanceRecords = await Db.AttendanceRecords
                .Where(a => a.ClassId == AssignedClass.Id && a.Date == today)
                .ToListAsync();

            PresentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present);
            AbsentCount = attendanceRecords.Count(a => a.Status is AttendanceStatus.AbsentWithLeave or AttendanceStatus.AbsentWithoutLeave);
            LateCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Late);

            // Lunch count today
            LunchCount = await Db.LunchRegistrations
                .CountAsync(l => studentIds.Contains(l.StudentId) && l.Date == today && l.Status == LunchRegistrationStatus.Registered);

            // Pending Leave requests
            PendingLeaveRequests = await Db.AdministrativeRequests
                .Where(r => r.RequestType == RequestType.Leave && studentIds.Contains(r.StudentId) && r.Status == RequestStatus.Submitted)
                .Include(r => r.Student)
                .Include(r => r.LeaveRequest)
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();
        }
    }
}
