using Microsoft.AspNetCore.Mvc.RazorPages;
using SAWP.Web.Data;
using SAWP.Web.Services;

namespace SAWP.Web.Infrastructure;

/// <summary>
/// Common base class for all Razor PageModels in SAWP.
/// Exposes common helpers, CurrentUser, DbContext, and Toast notifications.
/// </summary>
public abstract class AppPageModel : PageModel
{
    private ICurrentUser? _currentUser;
    private SawpDbContext? _db;
    private IClock? _clock;

    public ICurrentUser CurrentUser => _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
    public SawpDbContext Db => _db ??= HttpContext.RequestServices.GetRequiredService<SawpDbContext>();
    public IClock Clock => _clock ??= HttpContext.RequestServices.GetRequiredService<IClock>();

    /// <summary>
    /// Sets a toast message using a standardized message code (MSG01 - MSG50) or custom text.
    /// </summary>
    protected void Toast(string msgCode, string? customText = null, string? toastType = null)
    {
        var text = customText ?? Messages.Get(msgCode);
        var type = toastType ?? (Messages.IsSuccess(msgCode) ? "success" : "error");

        TempData["ToastCode"] = msgCode;
        TempData["ToastMessage"] = text;
        TempData["ToastType"] = type;
    }

    /// <summary>
    /// Sets an explicit success toast message.
    /// </summary>
    protected void ToastSuccess(string message)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = "success";
    }

    /// <summary>
    /// Sets an explicit error toast message.
    /// </summary>
    protected void ToastError(string message)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = "error";
    }

    /// <summary>
    /// Formats a date using the school's configured timezone and standard format.
    /// </summary>
    public string FormatDate(DateTime date) => Clock.FormatDate(date);

    /// <summary>
    /// Formats date and time.
    /// </summary>
    public string FormatDateTime(DateTime dateTime) => Clock.FormatDateTime(dateTime);
}
