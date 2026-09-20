namespace Purch.Application.Auth;

/// <summary>What an admin's email-login password must satisfy. Length is what matters most for a
/// password that guards the whole business; the upper bound only keeps hashing cost bounded.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    /// <summary>Returns a message describing what is wrong with the password, or null if it is acceptable.</summary>
    public static string? Validate(string? password)
    {
        return string.IsNullOrWhiteSpace(password)
            ? "Password is required."
            : password.Length is < MinLength or > MaxLength
                ? $"Password must be {MinLength}-{MaxLength} characters."
                : null;
    }
}
