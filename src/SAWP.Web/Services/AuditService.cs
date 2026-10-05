using SAWP.Web.Data;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// Service for creating append-only audit logs (BR-11, BR-45, BR-49, UC-58).
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? entityId = null, string? details = null);
}

/// <summary>
/// Implementation of IAuditService.
/// </summary>
public class AuditService : IAuditService
{
    private readonly SawpDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        SawpDbContext db,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityName, string? entityId = null, string? details = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ip = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();

        var entry = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            ActorUserId = _currentUser.UserId,
            ActorName = _currentUser.FullName ?? _currentUser.Username ?? "System",
            ActorRole = _currentUser.Role ?? "System",
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = ip,
            UserAgent = userAgent != null && userAgent.Length > 250 ? userAgent[..250] : userAgent
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "[Audit] {Action} {EntityName} Id={EntityId} By={Actor} ({Role}) Details={Details}",
            action, entityName, entityId, entry.ActorName, entry.ActorRole, details);
    }
}
