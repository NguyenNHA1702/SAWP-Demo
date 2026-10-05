using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

namespace SAWP.Web.Pages.Parent.Requests;

public class IndexModel : AppPageModel
{
    private readonly IScopeService _scopeService;
    private readonly IRequestWorkflowService _workflowService;
    private readonly IAuditService _auditService;

    public IndexModel(
        IScopeService scopeService,
        IRequestWorkflowService workflowService,
        IAuditService auditService)
    {
        _scopeService = scopeService;
        _workflowService = workflowService;
        _auditService = auditService;
    }

    public List<Student> Children { get; set; } = new();
    public List<AdministrativeRequest> Requests { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? StudentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public RequestType? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public RequestStatus? Status { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return RedirectToPage("/Parent/Index");

        Children = await Db.StudentGuardians
            .Where(sg => sg.GuardianId == guardianId.Value)
            .Include(sg => sg.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Select(sg => sg.Student)
            .ToListAsync();

        var query = Db.AdministrativeRequests
            .Where(r => r.RequesterGuardianId == guardianId.Value)
            .Include(r => r.Student)
                .ThenInclude(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
            .Include(r => r.LeaveRequest)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.CurrentClass)
            .Include(r => r.ClassTransferDetail)
                .ThenInclude(ct => ct!.TargetClass)
            .Include(r => r.DocumentRequestDetail)
            .Include(r => r.StatusHistories)
            .AsQueryable();

        if (StudentId.HasValue)
        {
            query = query.Where(r => r.StudentId == StudentId.Value);
        }

        if (Type.HasValue)
        {
            query = query.Where(r => r.RequestType == Type.Value);
        }

        if (Status.HasValue)
        {
            query = query.Where(r => r.Status == Status.Value);
        }

        Requests = await query
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var guardianId = await CurrentUser.GetGuardianIdAsync();
        if (!guardianId.HasValue) return RedirectToPage("/Parent/Index");

        var request = await Db.AdministrativeRequests.FirstOrDefaultAsync(r => r.Id == id && r.RequesterGuardianId == guardianId.Value);
        if (request == null)
        {
            Toast("MSG10");
            return RedirectToPage();
        }

        try
        {
            // BR-36: Requester can cancel before final decision
            await _workflowService.TransitionStatusAsync(
                id,
                RequestStatus.Cancelled,
                actionNotes: "Phụ huynh chủ động hủy yêu cầu");

            await _auditService.LogAsync(
                "CancelRequest",
                nameof(AdministrativeRequest),
                id.ToString(),
                $"Phụ huynh hủy yêu cầu {request.RequestCode}");

            Toast("MSG08", "Đã hủy yêu cầu thành công.", "success");
        }
        catch (Exception ex)
        {
            Toast("MSG34", ex.Message, "error");
        }

        return RedirectToPage(new { StudentId, Type, Status });
    }
}
