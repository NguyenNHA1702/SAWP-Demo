using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// System settings management (UC-59).
/// Provides strongly-typed access to dynamic configuration like Leave.MaxDaysForTeacher.
/// </summary>
public interface ISettingsService
{
    Task<string> GetStringAsync(string key, string defaultValue = "");
    Task<int> GetIntAsync(string key, int defaultValue = 0);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false);
    Task<TimeSpan> GetTimeSpanAsync(string key, TimeSpan defaultValue);
    Task SetAsync(string key, string value, string? description = null, string category = "General");
    Task<List<SystemSetting>> GetAllAsync();
}

/// <summary>
/// Implementation of ISettingsService backed by EF Core.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly SawpDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(SawpDbContext db, ICurrentUser currentUser, ILogger<SettingsService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<string> GetStringAsync(string key, string defaultValue = "")
    {
        var setting = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value ?? defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0)
    {
        var val = await GetStringAsync(key);
        return int.TryParse(val, out var res) ? res : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var val = await GetStringAsync(key);
        return bool.TryParse(val, out var res) ? res : defaultValue;
    }

    public async Task<TimeSpan> GetTimeSpanAsync(string key, TimeSpan defaultValue)
    {
        var val = await GetStringAsync(key);
        return TimeSpan.TryParse(val, out var res) ? res : defaultValue;
    }

    public async Task SetAsync(string key, string value, string? description = null, string category = "General")
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Value = value,
                Description = description,
                Category = category,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = _currentUser.Username ?? "System"
            };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            if (description != null) setting.Description = description;
            setting.Category = category;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = _currentUser.Username ?? "System";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Settings] Key={Key} updated to Value={Value} by {User}", key, value, setting.UpdatedBy);
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        return await _db.SystemSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync();
    }
}
