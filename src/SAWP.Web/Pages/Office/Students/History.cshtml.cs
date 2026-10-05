using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Office.Students;

public class HistoryModel : AppPageModel
{
    private readonly IScopeService _scopeService;

    public HistoryModel(IScopeService scopeService)
    {
        _scopeService = scopeService;
    }

    public Student Student { get; set; } = null!;
    public List<TimelineEventViewModel> TimelineEvents { get; set; } = new();

    public class TimelineEventViewModel
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "badge-slate";
        public string IconName { get; set; } = "clock";
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await _scopeService.CanAccessStudentAsync(id))
        {
            Toast("MSG10");
            return RedirectToPage("/Office/Students/Index");
        }

        var student = await Db.Students
            .Include(s => s.ClassAssignments)
            .ThenInclude(ca => ca.Class)
            .Include(s => s.AdministrativeRequests)
            .ThenInclude(r => r.LeaveRequest)
            .Include(s => s.AdministrativeRequests)
            .ThenInclude(r => r.ClassTransferDetail)
            .Include(s => s.AdministrativeRequests)
            .ThenInclude(r => r.DocumentRequestDetail)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            Toast("MSG11");
            return RedirectToPage("/Office/Students/Index");
        }

        Student = student;
        var events = new List<TimelineEventViewModel>();

        // 1. Admission Event
        events.Add(new TimelineEventViewModel
        {
            Timestamp = student.AdmissionDate,
            EventType = "Nhập học",
            Title = "Hồ sơ nhập học được tạo trong hệ thống",
            Description = $"Học sinh {student.FullName} được ghi nhận vào hệ thống với mã {student.StudentCode}.",
            BadgeClass = "badge-mint",
            IconName = "graduation-cap"
        });

        // 2. Class Assignments
        foreach (var ca in student.ClassAssignments)
        {
            events.Add(new TimelineEventViewModel
            {
                Timestamp = ca.StartDate,
                EventType = "Phân lớp",
                Title = $"Phân công vào Lớp {ca.Class.Name} (Khối {ca.Class.Grade})",
                Description = $"Bắt đầu học tại lớp {ca.Class.Name}. Sĩ số tối đa: {ca.Class.Capacity}. {ca.TransferNote}",
                BadgeClass = "badge-violet",
                IconName = "book-open"
            });
        }

        // 3. Administrative Requests
        foreach (var req in student.AdministrativeRequests)
        {
            var reqTypeStr = req.RequestType switch
            {
                RequestType.Leave => "Nghỉ học",
                RequestType.ClassTransfer => "Chuyển lớp",
                _ => "Cấp giấy tờ"
            };

            events.Add(new TimelineEventViewModel
            {
                Timestamp = req.SubmittedAt,
                EventType = reqTypeStr,
                Title = $"Gửi yêu cầu {reqTypeStr} ({req.RequestCode})",
                Description = $"Trạng thái hiện tại: {req.Status}. {req.Notes}",
                BadgeClass = req.Status == RequestStatus.Approved ? "badge-mint" : "badge-amber",
                IconName = "file-text"
            });
        }

        // Sort chronological descending
        TimelineEvents = events.OrderByDescending(e => e.Timestamp).ToList();

        return Page();
    }
}
