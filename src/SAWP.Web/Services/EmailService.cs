using SAWP.Web.Data;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// Simulated email dispatcher writing to EmailOutbox table (UC-01..05, Admin inspection).
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, string? otpCode = null);
}

/// <summary>
/// Implementation of IEmailService.
/// </summary>
public class EmailService : IEmailService
{
    private readonly SawpDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(SawpDbContext db, ILogger<EmailService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, string? otpCode = null)
    {
        var outbox = new EmailOutbox
        {
            ToEmail = toEmail,
            Subject = subject,
            Body = body,
            OtpCode = otpCode,
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow,
            Status = "Sent"
        };

        _db.EmailOutboxes.Add(outbox);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Email Simulated] To={To} Subject={Subject} OTP={Otp}", toEmail, subject, otpCode ?? "None");
    }
}
