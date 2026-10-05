namespace SAWP.Web.Services;

/// <summary>
/// School clock abstraction respecting BR-53 (School timezone SE Asia Standard Time / Asia/Ho_Chi_Minh).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateTime Today { get; }
    TimeZoneInfo TimeZone { get; }
    string FormatDate(DateTime date);
    string FormatDateTime(DateTime dateTime);
    DateTime ToLocal(DateTime utcDateTime);
    DateTime ToUtc(DateTime localDateTime);
}

/// <summary>
/// Implementation of IClock using SE Asia Standard Time.
/// </summary>
public class ClockService : IClock
{
    private readonly TimeZoneInfo _timeZone;

    public ClockService()
    {
        try
        {
            // Windows ID
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                // Linux / IANA ID
                _timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                _timeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "SE Asia Standard Time",
                    TimeSpan.FromHours(7),
                    "SE Asia Standard Time",
                    "SE Asia Standard Time");
            }
        }
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

    public DateTime Today => LocalNow.Date;

    public TimeZoneInfo TimeZone => _timeZone;

    public string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy");

    public string FormatDateTime(DateTime dateTime) => dateTime.ToString("dd/MM/yyyy HH:mm");

    public DateTime ToLocal(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime.ToUniversalTime(), _timeZone);
    }

    public DateTime ToUtc(DateTime localDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _timeZone);
    }
}
