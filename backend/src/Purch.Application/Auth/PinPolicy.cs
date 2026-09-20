namespace Purch.Application.Auth;

/// <summary>What a staff PIN may look like. The PIN is the only credential a cashier types at the
/// counter, so it has to be all digits and long enough to not be trivially guessable; a PIN that
/// another staff member already uses is refused by the caller (it needs the tenant's users).</summary>
public static class PinPolicy
{
    public const int MinLength = 4;
    public const int MaxLength = 8;

    /// <summary>Returns a message describing what is wrong with the PIN, or null if it is acceptable.</summary>
    public static string? Validate(string? pin)
    {
        return string.IsNullOrWhiteSpace(pin)
            ? "PIN is required."
            : pin.Length is < MinLength or > MaxLength || !pin.All(char.IsAsciiDigit)
                ? $"PIN must be {MinLength}-{MaxLength} digits."
                : null;
    }
}
