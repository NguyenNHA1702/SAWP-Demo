using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using SAWP.Web.Domain;

namespace SAWP.Web.Services;

/// <summary>
/// Service for hashing passwords and validating complexity according to BR-02.
/// </summary>
public interface IPasswordService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string providedPassword);
    bool ValidateComplexity(string password, out string? errorMessage);
}

/// <summary>
/// Implementation of IPasswordService using ASP.NET Core PasswordHasher.
/// </summary>
public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
        return result != PasswordVerificationResult.Failed;
    }

    /// <summary>
    /// Enforces BR-02: At least 8 characters, uppercase, lowercase, digit, and special character.
    /// </summary>
    public bool ValidateComplexity(string password, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            errorMessage = Messages.Get("MSG04");
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
        {
            errorMessage = Messages.Get("MSG04");
            return false;
        }

        return true;
    }
}
